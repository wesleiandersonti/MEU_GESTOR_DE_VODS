import {
  Controller,
  Get,
  Post,
  Put,
  Delete,
  Body,
  Param,
  Query,
  UseGuards,
  Request,
  ParseIntPipe,
  HttpCode,
  HttpStatus,
} from '@nestjs/common';
import { ApiTags, ApiOperation, ApiResponse, ApiBearerAuth, ApiQuery } from '@nestjs/swagger';
import { JwtAuthGuard } from '../../auth/guards/jwt-auth.guard';
import { RolesGuard } from '../../auth/guards/roles.guard';
import { Roles } from '../../auth/decorators/roles.decorator';
import { UserRole } from '../users/entities/user.entity';
import { ApplicationsService, CreateApplicationDto, UpdateApplicationDto } from './applications.service';
import { Application, ApplicationStatus } from './entities/application.entity';
import { RequestWithUser } from '../../auth/interfaces/request-with-user.interface';

@ApiTags('Applications')
@Controller('applications')
@UseGuards(JwtAuthGuard, RolesGuard)
@ApiBearerAuth()
export class ApplicationsController {
  constructor(private readonly applicationsService: ApplicationsService) {}

  @Get()
  @ApiOperation({ summary: 'Get all applications for tenant' })
  @ApiQuery({ name: 'status', required: false, enum: ApplicationStatus })
  @ApiResponse({ status: 200, description: 'List of applications', type: [Application] })
  async findAll(
    @Request() req: RequestWithUser,
    @Query('status') status?: ApplicationStatus,
  ): Promise<Application[]> {
    const options = status ? { where: { status } } : {};
    return this.applicationsService.findAll(req.user.tenantId, options);
  }

  @Get('stats')
  @ApiOperation({ summary: 'Get application statistics' })
  @ApiResponse({ status: 200, description: 'Application statistics' })
  async getStats(@Request() req: RequestWithUser): Promise<any> {
    return this.applicationsService.getStats(req.user.tenantId);
  }

  @Get(':id')
  @ApiOperation({ summary: 'Get application by ID' })
  @ApiResponse({ status: 200, description: 'Application details', type: Application })
  @ApiResponse({ status: 404, description: 'Application not found' })
  async findOne(
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<Application> {
    return this.applicationsService.findOne(id, req.user.tenantId);
  }

  @Post()
  @Roles(UserRole.ADMIN, UserRole.DEVOPS, UserRole.DEVELOPER)
  @ApiOperation({ summary: 'Create new application' })
  @ApiResponse({ status: 201, description: 'Application created', type: Application })
  @ApiResponse({ status: 403, description: 'Slug already exists' })
  async create(
    @Body() createDto: CreateApplicationDto,
    @Request() req: RequestWithUser,
  ): Promise<Application> {
    return this.applicationsService.create(createDto, req.user.tenantId, req.user.id);
  }

  @Put(':id')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Update application' })
  @ApiResponse({ status: 200, description: 'Application updated', type: Application })
  async update(
    @Param('id', ParseIntPipe) id: number,
    @Body() updateDto: UpdateApplicationDto,
    @Request() req: RequestWithUser,
  ): Promise<Application> {
    return this.applicationsService.update(id, updateDto, req.user.tenantId, req.user);
  }

  @Delete(':id')
  @Roles(UserRole.ADMIN)
  @HttpCode(HttpStatus.NO_CONTENT)
  @ApiOperation({ summary: 'Delete application' })
  @ApiResponse({ status: 204, description: 'Application deleted' })
  async remove(
    @Param('id', ParseIntPipe) id: number,
    @Request() req: RequestWithUser,
  ): Promise<void> {
    return this.applicationsService.remove(id, req.user.tenantId, req.user);
  }

  @Post(':id/status')
  @Roles(UserRole.ADMIN, UserRole.DEVOPS)
  @ApiOperation({ summary: 'Update application status' })
  @ApiResponse({ status: 200, description: 'Status updated', type: Application })
  async updateStatus(
    @Param('id', ParseIntPipe) id: number,
    @Body('status') status: ApplicationStatus,
    @Request() req: RequestWithUser,
  ): Promise<Application> {
    return this.applicationsService.updateStatus(id, status, req.user.tenantId);
  }
}
