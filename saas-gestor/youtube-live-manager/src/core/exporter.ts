import fs from 'node:fs/promises';
import path from 'node:path';
import { env } from '../config/env';
import { ChannelRepository } from '../db/repositories/channelRepository';
import { ExportRepository } from '../db/repositories/exportRepository';
import { sanitizeGroupTitle } from '../utils/channel';

export interface ExportResult {
  exportId: number;
  filePath: string;
  channelsCount: number;
  content: string;
}

export class M3UExporter {
  constructor(
    private readonly channelRepository: ChannelRepository,
    private readonly exportRepository: ExportRepository,
  ) {}

  async exportActiveChannels(): Promise<ExportResult> {
    const channels = await this.channelRepository.listOnlineForExport();

    const lines: string[] = ['#EXTM3U'];

    for (const channel of channels) {
      const groupTitle = sanitizeGroupTitle(channel.category);
      lines.push(`#EXTINF:-1 group-title="${groupTitle}",${channel.name} (YT Live)`);

      const streamLine = env.exportUseProxy
        ? `${env.publicBaseUrl.replace(/\/$/, '')}/stream/${channel.id}`
        : channel.streamUrl;

      lines.push(streamLine);
    }

    const content = `${lines.join('\n')}\n`;

    const outputDir = path.resolve(process.cwd(), env.outputDir);
    await fs.mkdir(outputDir, { recursive: true });

    const filePath = path.join(outputDir, 'active_channels.m3u');
    await fs.writeFile(filePath, content, 'utf-8');

    const exportId = await this.exportRepository.createM3U(filePath, channels.length);

    return {
      exportId,
      filePath,
      channelsCount: channels.length,
      content,
    };
  }
}
