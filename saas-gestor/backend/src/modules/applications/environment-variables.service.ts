import { Injectable, NotFoundException, BadRequestException } from '@nestjs/common';
import { InjectRepository } from '@nestjs/typeorm';
import { Repository } from 'typeorm';
import * as crypto from 'crypto';
import {
  EnvironmentVariable,
  EnvironmentVariableScope,
} from './entities/environment-variable.entity';
import { ApplicationsService } from './applications.service';

// Encryption key should be in environment variables
const ENCRYPTION_KEY = process.env.ENV_ENCRYPTION_KEY || 'default-env-key-32chars-long!!12345';

export interface CreateEnvironmentVariableDto {
  key: string;
  value: string;
  scope?: EnvironmentVariableScope;
  isSecret?: boolean;
  description?: string;
}

export interface UpdateEnvironmentVariableDto {
  value?: string;
  scope?: EnvironmentVariableScope;
  isSecret?: boolean;
  description?: string;
  isActive?: boolean;
}

export interface EnvironmentVariableResponse {
  id: number;
  key: string;
  value: string | null; // Null if secret and not revealed
  scope: EnvironmentVariableScope;
  isSecret: boolean;
  isRevealed: boolean;
  description: string;
  isActive: boolean;
  createdAt: Date;
  updatedAt: Date;
}

@Injectable()
export class EnvironmentVariablesService {
  constructor(
    @InjectRepository(EnvironmentVariable)
    private envVarRepository: Repository<EnvironmentVariable>,
    private applicationsService: ApplicationsService,
  ) {}

  /**
   * Encrypt a value using AES-256
   */
  private encrypt(text: string): string {
    const iv = crypto.randomBytes(16);
    const cipher = crypto.createCipheriv(
      'aes-256-cbc',
      Buffer.from(ENCRYPTION_KEY.padEnd(32).slice(0, 32)),
      iv,
    );
    let encrypted = cipher.update(text, 'utf8', 'hex');
    encrypted += cipher.final('hex');
    return iv.toString('hex') + ':' + encrypted;
  }

  /**
   * Decrypt a value using AES-256
   */
  private decrypt(encryptedText: string): string {
    const parts = encryptedText.split(':');
    const iv = Buffer.from(parts[0], 'hex');
    const encrypted = parts[1];
    const decipher = crypto.createDecipheriv(
      'aes-256-cbc',
      Buffer.from(ENCRYPTION_KEY.padEnd(32).slice(0, 32)),
      iv,
    );
    let decrypted = decipher.update(encrypted, 'hex', 'utf8');
    decrypted += decipher.final('utf8');
    return decrypted;
  }

  /**
   * Get all environment variables for an application
   */
  async findAll(
    applicationId: number,
    tenantId: number,
    revealSecrets: boolean = false,
  ): Promise<EnvironmentVariableResponse[]> {
    // Verify application belongs to tenant
    await this.applicationsService.findOne(applicationId, tenantId);

    const envVars = await this.envVarRepository.find({
      where: { applicationId },
      order: { key: 'ASC' },
    });

    return envVars.map((env) => this.mapToResponse(env, revealSecrets));
  }

  /**
   * Get environment variable by ID
   */
  async findOne(
    id: number,
    applicationId: number,
    tenantId: number,
    revealSecret: boolean = false,
  ): Promise<EnvironmentVariableResponse> {
    // Verify application belongs to tenant
    await this.applicationsService.findOne(applicationId, tenantId);

    const envVar = await this.envVarRepository.findOne({
      where: { id, applicationId },
    });

    if (!envVar) {
      throw new NotFoundException('Environment variable not found');
    }

    return this.mapToResponse(envVar, revealSecret);
  }

  /**
   * Create new environment variable
   */
  async create(
    applicationId: number,
    createDto: CreateEnvironmentVariableDto,
    tenantId: number,
    userId: number,
  ): Promise<EnvironmentVariableResponse> {
    // Verify application belongs to tenant
    await this.applicationsService.findOne(applicationId, tenantId);

    // Validate key format (alphanumeric + underscore, starting with letter)
    if (!/^[a-zA-Z_][a-zA-Z0-9_]*$/.test(createDto.key)) {
      throw new BadRequestException(
        'Key must start with a letter or underscore and contain only alphanumeric characters and underscores',
      );
    }

    // Check for duplicate key
    const existing = await this.envVarRepository.findOne({
      where: { applicationId, key: createDto.key },
    });

    if (existing) {
      throw new BadRequestException(`Environment variable '${createDto.key}' already exists`);
    }

    // Encrypt value if it's a secret
    const value = createDto.isSecret
      ? this.encrypt(createDto.value)
      : createDto.value;

    const envVar = this.envVarRepository.create({
      applicationId,
      key: createDto.key.toUpperCase(), // Store keys in uppercase
      value,
      scope: createDto.scope || EnvironmentVariableScope.APPLICATION,
      isSecret: createDto.isSecret || false,
      description: createDto.description,
      createdById: userId,
    });

    const saved = await this.envVarRepository.save(envVar);
    return this.mapToResponse(saved, false);
  }

  /**
   * Create multiple environment variables at once
   */
  async createMany(
    applicationId: number,
    variables: CreateEnvironmentVariableDto[],
    tenantId: number,
    userId: number,
  ): Promise<EnvironmentVariableResponse[]> {
    const results: EnvironmentVariableResponse[] = [];

    for (const variable of variables) {
      try {
        const result = await this.create(applicationId, variable, tenantId, userId);
        results.push(result);
      } catch (error) {
        // Log error but continue with other variables
        console.error(`Failed to create env var ${variable.key}:`, error.message);
      }
    }

    return results;
  }

  /**
   * Update environment variable
   */
  async update(
    id: number,
    applicationId: number,
    updateDto: UpdateEnvironmentVariableDto,
    tenantId: number,
  ): Promise<EnvironmentVariableResponse> {
    const envVar = await this.findEntity(id, applicationId, tenantId);

    // If value is being updated
    if (updateDto.value !== undefined) {
      envVar.value = envVar.isSecret
        ? this.encrypt(updateDto.value)
        : updateDto.value;
    }

    // Update other fields
    if (updateDto.scope !== undefined) {
      envVar.scope = updateDto.scope;
    }

    if (updateDto.isSecret !== undefined) {
      // If switching from non-secret to secret, encrypt
      if (updateDto.isSecret && !envVar.isSecret) {
        envVar.value = this.encrypt(envVar.value);
      }
      // If switching from secret to non-secret, decrypt
      if (!updateDto.isSecret && envVar.isSecret) {
        envVar.value = this.decrypt(envVar.value);
      }
      envVar.isSecret = updateDto.isSecret;
    }

    if (updateDto.description !== undefined) {
      envVar.description = updateDto.description;
    }

    if (updateDto.isActive !== undefined) {
      envVar.isActive = updateDto.isActive;
    }

    const saved = await this.envVarRepository.save(envVar);
    return this.mapToResponse(saved, false);
  }

  /**
   * Delete environment variable
   */
  async remove(
    id: number,
    applicationId: number,
    tenantId: number,
  ): Promise<void> {
    const envVar = await this.findEntity(id, applicationId, tenantId);
    await this.envVarRepository.remove(envVar);
  }

  /**
   * Delete all environment variables for an application
   */
  async removeAll(applicationId: number, tenantId: number): Promise<void> {
    await this.applicationsService.findOne(applicationId, tenantId);
    await this.envVarRepository.delete({ applicationId });
  }

  /**
   * Get environment variables formatted for build
   */
  async getForBuild(
    applicationId: number,
    tenantId: number,
  ): Promise<Record<string, string>> {
    const envVars = await this.findAll(applicationId, tenantId, true);

    return envVars
      .filter((env) => env.isActive && 
        (env.scope === EnvironmentVariableScope.BUILD || 
         env.scope === EnvironmentVariableScope.APPLICATION))
      .reduce((acc, env) => {
        acc[env.key] = env.value || '';
        return acc;
      }, {} as Record<string, string>);
  }

  /**
   * Get environment variables formatted for runtime (deployment)
   */
  async getForRuntime(
    applicationId: number,
    tenantId: number,
  ): Promise<Record<string, string>> {
    const envVars = await this.findAll(applicationId, tenantId, true);

    return envVars
      .filter((env) => env.isActive && 
        (env.scope === EnvironmentVariableScope.RUNTIME || 
         env.scope === EnvironmentVariableScope.APPLICATION))
      .reduce((acc, env) => {
        acc[env.key] = env.value || '';
        return acc;
      }, {} as Record<string, string>);
  }

  /**
   * Clone environment variables from one application to another
   */
  async cloneVariables(
    sourceApplicationId: number,
    targetApplicationId: number,
    tenantId: number,
    userId: number,
  ): Promise<{ cloned: number; failed: number }> {
    const sourceVars = await this.findAll(sourceApplicationId, tenantId, true);
    let cloned = 0;
    let failed = 0;

    for (const sourceVar of sourceVars) {
      try {
        await this.create(targetApplicationId, {
          key: sourceVar.key,
          value: sourceVar.value || '',
          scope: sourceVar.scope,
          isSecret: sourceVar.isSecret,
          description: sourceVar.description
            ? `${sourceVar.description} (cloned)`
            : 'Cloned from another application',
        }, tenantId, userId);
        cloned++;
      } catch (error) {
        failed++;
        console.error(`Failed to clone env var ${sourceVar.key}:`, error.message);
      }
    }

    return { cloned, failed };
  }

  /**
   * Import environment variables from .env file format
   */
  async importFromEnvFile(
    applicationId: number,
    envFileContent: string,
    tenantId: number,
    userId: number,
    markAsSecret: boolean = false,
  ): Promise<{ imported: number; failed: number; errors: string[] }> {
    const lines = envFileContent.split('\n');
    const errors: string[] = [];
    let imported = 0;
    let failed = 0;

    for (const line of lines) {
      const trimmed = line.trim();

      // Skip empty lines and comments
      if (!trimmed || trimmed.startsWith('#')) {
        continue;
      }

      // Parse KEY=VALUE format
      const equalIndex = trimmed.indexOf('=');
      if (equalIndex === -1) {
        continue;
      }

      const key = trimmed.substring(0, equalIndex).trim();
      let value = trimmed.substring(equalIndex + 1).trim();

      // Remove quotes if present
      if ((value.startsWith('"') && value.endsWith('"')) ||
          (value.startsWith("'") && value.endsWith("'"))) {
        value = value.slice(1, -1);
      }

      try {
        await this.create(
          applicationId,
          {
            key,
            value,
            isSecret: markAsSecret,
            description: `Imported from .env file`,
          },
          tenantId,
          userId,
        );
        imported++;
      } catch (error) {
        failed++;
        errors.push(`${key}: ${error.message}`);
      }
    }

    return { imported, failed, errors };
  }

  /**
   * Export environment variables to .env file format
   */
  async exportToEnvFile(
    applicationId: number,
    tenantId: number,
    includeSecrets: boolean = false,
  ): Promise<string> {
    const envVars = await this.findAll(applicationId, tenantId, includeSecrets);
    const activeVars = envVars.filter((env) => env.isActive);

    const lines: string[] = ['# Environment Variables', `# Generated at ${new Date().toISOString()}`, ''];

    for (const env of activeVars) {
      if (env.description) {
        lines.push(`# ${env.description}`);
      }

      const value = env.value || '';
      // Quote value if it contains spaces or special characters
      const needsQuotes = /\s|"|'/.test(value);
      lines.push(`${env.key}=${needsQuotes ? `"${value}"` : value}`);
      lines.push('');
    }

    return lines.join('\n');
  }

  /**
   * Bulk update environment variables
   */
  async bulkUpdate(
    applicationId: number,
    variables: Array<{ id?: number; key: string; value: string; isSecret?: boolean }>,
    tenantId: number,
    userId: number,
  ): Promise<{ created: number; updated: number; deleted: number }> {
    await this.applicationsService.findOne(applicationId, tenantId);

    let created = 0;
    let updated = 0;
    let deleted = 0;

    // Get existing variables
    const existingVars = await this.envVarRepository.find({
      where: { applicationId },
    });
    const existingKeys = new Set(existingVars.map((v) => v.key));
    const updatedKeys = new Set<string>();

    for (const variable of variables) {
      if (variable.id) {
        // Update existing
        try {
          await this.update(
            variable.id,
            applicationId,
            { value: variable.value, isSecret: variable.isSecret },
            tenantId,
          );
          updated++;
          updatedKeys.add(variable.key.toUpperCase());
        } catch (error) {
          console.error(`Failed to update ${variable.key}:`, error.message);
        }
      } else {
        // Create new
        try {
          await this.create(
            applicationId,
            {
              key: variable.key,
              value: variable.value,
              isSecret: variable.isSecret || false,
            },
            tenantId,
            userId,
          );
          created++;
          updatedKeys.add(variable.key.toUpperCase());
        } catch (error) {
          console.error(`Failed to create ${variable.key}:`, error.message);
        }
      }
    }

    // Delete variables not in the update list
    for (const existing of existingVars) {
      if (!updatedKeys.has(existing.key)) {
        await this.envVarRepository.remove(existing);
        deleted++;
      }
    }

    return { created, updated, deleted };
  }

  // Private helper methods

  private async findEntity(
    id: number,
    applicationId: number,
    tenantId: number,
  ): Promise<EnvironmentVariable> {
    await this.applicationsService.findOne(applicationId, tenantId);

    const envVar = await this.envVarRepository.findOne({
      where: { id, applicationId },
    });

    if (!envVar) {
      throw new NotFoundException('Environment variable not found');
    }

    return envVar;
  }

  private mapToResponse(
    envVar: EnvironmentVariable,
    revealSecret: boolean,
  ): EnvironmentVariableResponse {
    let value: string | null = envVar.value;
    let isRevealed = false;

    if (envVar.isSecret) {
      if (revealSecret) {
        try {
          value = this.decrypt(envVar.value);
          isRevealed = true;
        } catch {
          value = null;
        }
      } else {
        value = null;
      }
    }

    return {
      id: envVar.id,
      key: envVar.key,
      value,
      scope: envVar.scope,
      isSecret: envVar.isSecret,
      isRevealed,
      description: envVar.description,
      isActive: envVar.isActive,
      createdAt: envVar.createdAt,
      updatedAt: envVar.updatedAt,
    };
  }
}
