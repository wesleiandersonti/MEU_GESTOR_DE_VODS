import { Injectable, UnauthorizedException, BadRequestException, Inject } from '@nestjs/common';
import { JwtService } from '@nestjs/jwt';
import { ConfigService } from '@nestjs/config';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import { CACHE_MANAGER } from '@nestjs/cache-manager';
import { Cache } from 'cache-manager';
import * as bcrypt from 'bcrypt';
import { User, UserStatus } from '../modules/users/entities/user.entity';
import { Tenant, TenantStatus } from '../modules/tenants/entities/tenant.entity';

export interface JwtPayload {
  sub: number;
  email: string;
  tenantId: number;
  role: string;
  iat?: number;
  exp?: number;
}

export interface LoginResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
  user: {
    id: number;
    name: string;
    email: string;
    role: string;
    tenantId: number;
    tenantName: string;
  };
}

@Injectable()
export class AuthService {
  constructor(
    @InjectRepository(User)
    private userRepository: Repository<User>,
    @InjectRepository(Tenant)
    private tenantRepository: Repository<Tenant>,
    private jwtService: JwtService,
    private configService: ConfigService,
    @Inject(CACHE_MANAGER) private cacheManager: Cache,
  ) {}

  async validateUser(email: string, password: string, tenantSlug?: string): Promise<User | null> {
    const query = this.userRepository.createQueryBuilder('user')
      .leftJoinAndSelect('user.tenant', 'tenant')
      .where('user.email = :email', { email });

    if (tenantSlug) {
      query.andWhere('tenant.slug = :tenantSlug', { tenantSlug });
    }

    const user = await query.getOne();

    if (!user) {
      return null;
    }

    if (user.status !== UserStatus.ACTIVE) {
      throw new UnauthorizedException('User account is not active');
    }

    if (user.tenant.status !== TenantStatus.ACTIVE && user.tenant.status !== TenantStatus.TRIAL) {
      throw new UnauthorizedException('Tenant is not active');
    }

    const isPasswordValid = await bcrypt.compare(password, user.password);
    if (!isPasswordValid) {
      return null;
    }

    // Update last login
    user.lastLoginAt = new Date();
    await this.userRepository.save(user);

    return user;
  }

  async login(user: User): Promise<LoginResponse> {
    const payload: JwtPayload = {
      sub: user.id,
      email: user.email,
      tenantId: user.tenantId,
      role: user.role,
    };

    const accessToken = this.jwtService.sign(payload);
    const refreshToken = this.jwtService.sign(payload, {
      secret: this.configService.get<string>('JWT_REFRESH_SECRET'),
      expiresIn: this.configService.get<string>('JWT_REFRESH_EXPIRATION', '7d'),
    });

    return {
      accessToken,
      refreshToken,
      expiresIn: 3600, // 1 hour
      user: {
        id: user.id,
        name: user.name,
        email: user.email,
        role: user.role,
        tenantId: user.tenantId,
        tenantName: user.tenant.name,
      },
    };
  }

  async refreshToken(refreshToken: string): Promise<LoginResponse> {
    try {
      const payload = this.jwtService.verify(refreshToken, {
        secret: this.configService.get<string>('JWT_REFRESH_SECRET'),
      });

      const user = await this.userRepository.findOne({
        where: { id: payload.sub },
        relations: ['tenant'],
      });

      if (!user || user.status !== UserStatus.ACTIVE) {
        throw new UnauthorizedException('Invalid refresh token');
      }

      return this.login(user);
    } catch (error) {
      throw new UnauthorizedException('Invalid refresh token');
    }
  }

  async register(
    tenantName: string,
    tenantSlug: string,
    userName: string,
    email: string,
    password: string,
  ): Promise<LoginResponse> {
    // Check if tenant slug exists
    const existingTenant = await this.tenantRepository.findOne({
      where: { slug: tenantSlug },
    });

    if (existingTenant) {
      throw new BadRequestException('Tenant slug already exists');
    }

    // Check if email exists
    const existingUser = await this.userRepository.findOne({
      where: { email },
    });

    if (existingUser) {
      throw new BadRequestException('Email already registered');
    }

    // Create tenant
    const tenant = this.tenantRepository.create({
      name: tenantName,
      slug: tenantSlug,
    });
    await this.tenantRepository.save(tenant);

    // Create user
    const hashedPassword = await bcrypt.hash(password, 10);
    const user = this.userRepository.create({
      tenantId: tenant.id,
      name: userName,
      email,
      password: hashedPassword,
      status: UserStatus.ACTIVE,
    });
    await this.userRepository.save(user);

    return this.login(user);
  }

  async changePassword(userId: number, oldPassword: string, newPassword: string): Promise<void> {
    const user = await this.userRepository.findOne({ where: { id: userId } });

    if (!user) {
      throw new UnauthorizedException('User not found');
    }

    const isOldPasswordValid = await bcrypt.compare(oldPassword, user.password);
    if (!isOldPasswordValid) {
      throw new BadRequestException('Old password is incorrect');
    }

    user.password = await bcrypt.hash(newPassword, 10);
    await this.userRepository.save(user);
    
    // Clear user cache after password change
    await this.cacheManager.del(`user:${userId}`);
  }

  /**
   * Get user from cache or database
   * Demonstrates Redis cache usage
   */
  async getUserFromCache(userId: number): Promise<User | null> {
    const cacheKey = `user:${userId}`;
    
    // Try to get from cache
    const cachedUser = await this.cacheManager.get<User>(cacheKey);
    if (cachedUser) {
      console.log('✅ User found in cache');
      return cachedUser;
    }
    
    // If not in cache, get from database
    console.log('🔄 User not in cache, fetching from database...');
    const user = await this.userRepository.findOne({
      where: { id: userId },
      relations: ['tenant'],
    });
    
    if (user) {
      // Store in cache for 1 hour
      await this.cacheManager.set(cacheKey, user, 3600);
      console.log('💾 User cached for 1 hour');
    }
    
    return user;
  }

  /**
   * Get tenant stats with caching
   */
  async getTenantStats(tenantId: number): Promise<any> {
    const cacheKey = `tenant:stats:${tenantId}`;
    
    // Try to get from cache
    const cachedStats = await this.cacheManager.get(cacheKey);
    if (cachedStats) {
      return cachedStats;
    }
    
    // Calculate stats from database
    const stats = {
      totalUsers: await this.userRepository.count({ where: { tenantId } }),
      lastUpdated: new Date().toISOString(),
    };
    
    // Cache for 5 minutes
    await this.cacheManager.set(cacheKey, stats, 300);
    
    return stats;
  }

  /**
   * Clear all cache for a tenant
   */
  async clearTenantCache(tenantId: number): Promise<void> {
    // In a real app, you'd use a pattern to delete all keys matching tenant:*
    await this.cacheManager.del(`tenant:stats:${tenantId}`);
    console.log(`🧹 Cache cleared for tenant ${tenantId}`);
  }
}
