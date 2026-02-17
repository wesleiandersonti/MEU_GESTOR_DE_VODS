import { ChannelRepository } from '../db/repositories/channelRepository';
import { buildLiveUrl, normalizeYouTubeChannelUrl } from '../utils/channel';

export interface CatalogChannel {
  name: string;
  category: string;
  channelUrl: string;
}

export interface YouTubeLiveCatalog {
  source: {
    name: string;
    type: string;
    input: string;
    epg: string;
  };
  categories: string[];
  channels: CatalogChannel[];
  bouquets: Record<string, string[]>;
  users: Record<string, string[]>;
}

export const defaultYouTubeLiveCatalog: YouTubeLiveCatalog = {
  source: {
    name: 'YouTube Live',
    type: 'LIVE',
    input: 'DYNAMIC',
    epg: 'OFF',
  },
  categories: [
    'NOTICIAS_BRASIL',
    'ESPORTES_AO_VIVO',
    'MUSICA_INTERNACIONAL',
    'RADIOS_AO_VIVO',
    'NOTICIAS_INTERNACIONAL',
    'VARIADOS',
  ],
  channels: [
    { name: 'CNN Brasil', category: 'NOTICIAS_BRASIL', channelUrl: 'https://www.youtube.com/@CNNBrasil' },
    { name: 'BandNews TV', category: 'NOTICIAS_BRASIL', channelUrl: 'https://www.youtube.com/@BandNewsTV' },
    { name: 'Jovem Pan News', category: 'NOTICIAS_BRASIL', channelUrl: 'https://www.youtube.com/@JovemPanNews' },
    { name: 'Record News', category: 'NOTICIAS_BRASIL', channelUrl: 'https://www.youtube.com/@recordnews' },
    { name: 'SBT News', category: 'NOTICIAS_BRASIL', channelUrl: 'https://www.youtube.com/@sbtnews' },
    { name: 'TV Brasil', category: 'NOTICIAS_BRASIL', channelUrl: 'https://www.youtube.com/@tvbrasil' },
    { name: 'GloboNews', category: 'NOTICIAS_BRASIL', channelUrl: 'https://www.youtube.com/@GloboNews' },

    { name: 'CazeTV', category: 'ESPORTES_AO_VIVO', channelUrl: 'https://www.youtube.com/@CazeTV' },
    { name: 'Canal GOAT', category: 'ESPORTES_AO_VIVO', channelUrl: 'https://www.youtube.com/@CanalGOATBR' },
    { name: 'TNT Sports Brasil', category: 'ESPORTES_AO_VIVO', channelUrl: 'https://www.youtube.com/@TNTSportsBR' },
    { name: 'Jovem Pan Esportes', category: 'ESPORTES_AO_VIVO', channelUrl: 'https://www.youtube.com/@JovemPanEsportes' },
    { name: 'Esporte na Band', category: 'ESPORTES_AO_VIVO', channelUrl: 'https://www.youtube.com/@EsportenaBand' },
    { name: 'ESPN Brasil', category: 'ESPORTES_AO_VIVO', channelUrl: 'https://www.youtube.com/@ESPNBrasil' },

    { name: 'Live Nation', category: 'MUSICA_INTERNACIONAL', channelUrl: 'https://www.youtube.com/@LiveNation' },
    { name: 'Boiler Room', category: 'MUSICA_INTERNACIONAL', channelUrl: 'https://www.youtube.com/@boilerroom' },
    { name: 'Cercle', category: 'MUSICA_INTERNACIONAL', channelUrl: 'https://www.youtube.com/@Cercle' },
    { name: 'Tomorrowland', category: 'MUSICA_INTERNACIONAL', channelUrl: 'https://www.youtube.com/@tomorrowland' },
    { name: 'Ultra Music Festival', category: 'MUSICA_INTERNACIONAL', channelUrl: 'https://www.youtube.com/@UMFTV' },
    { name: 'VEVO Live', category: 'MUSICA_INTERNACIONAL', channelUrl: 'https://www.youtube.com/@VEVO' },

    { name: 'Jovem Pan FM', category: 'RADIOS_AO_VIVO', channelUrl: 'https://www.youtube.com/@JovemPanFM' },
    { name: 'Band FM', category: 'RADIOS_AO_VIVO', channelUrl: 'https://www.youtube.com/@BandFM' },
    { name: 'Mix FM', category: 'RADIOS_AO_VIVO', channelUrl: 'https://www.youtube.com/@radiomixfm' },
    { name: 'CBN', category: 'RADIOS_AO_VIVO', channelUrl: 'https://www.youtube.com/@CBN' },
    { name: 'Antena 1', category: 'RADIOS_AO_VIVO', channelUrl: 'https://www.youtube.com/@Antena1FM' },

    { name: 'BBC News', category: 'NOTICIAS_INTERNACIONAL', channelUrl: 'https://www.youtube.com/@BBCNews' },
    { name: 'Al Jazeera English', category: 'NOTICIAS_INTERNACIONAL', channelUrl: 'https://www.youtube.com/@AlJazeeraEnglish' },
    { name: 'DW News', category: 'NOTICIAS_INTERNACIONAL', channelUrl: 'https://www.youtube.com/@dwnews' },
    { name: 'France 24 English', category: 'NOTICIAS_INTERNACIONAL', channelUrl: 'https://www.youtube.com/@France24_en' },
    { name: 'Sky News', category: 'NOTICIAS_INTERNACIONAL', channelUrl: 'https://www.youtube.com/@SkyNews' },

    { name: 'DiaTV', category: 'VARIADOS', channelUrl: 'https://www.youtube.com/@DiaTV' },
    { name: 'Flow Podcast', category: 'VARIADOS', channelUrl: 'https://www.youtube.com/@FlowPodcast' },
    { name: 'Podpah', category: 'VARIADOS', channelUrl: 'https://www.youtube.com/@Podpah' },
  ],
  bouquets: {
    BQT_NOTICIAS: ['NOTICIAS_BRASIL', 'NOTICIAS_INTERNACIONAL'],
    BQT_ESPORTES: ['ESPORTES_AO_VIVO'],
    BQT_MUSICA: ['MUSICA_INTERNACIONAL'],
    BQT_RADIOS: ['RADIOS_AO_VIVO'],
    BQT_VARIADOS: ['VARIADOS'],
  },
  users: {
    USUARIO_FAMILIA: ['BQT_NOTICIAS', 'BQT_MUSICA', 'BQT_RADIOS', 'BQT_VARIADOS'],
    USUARIO_PREMIUM: ['BQT_NOTICIAS', 'BQT_ESPORTES', 'BQT_MUSICA', 'BQT_RADIOS', 'BQT_VARIADOS'],
  },
};

function normalizeCatalogUrl(url: string): string {
  const trimmed = url.trim();
  const withoutLive = trimmed.replace(/\/live\/?$/i, '');
  return normalizeYouTubeChannelUrl(withoutLive);
}

export async function seedDefaultYouTubeChannels(channelRepository: ChannelRepository): Promise<{
  imported: number;
  categories: number;
  channels: number;
}> {
  let imported = 0;

  for (const channel of defaultYouTubeLiveCatalog.channels) {
    const channelUrl = normalizeCatalogUrl(channel.channelUrl);
    const liveUrl = buildLiveUrl(channelUrl);

    await channelRepository.upsertByChannelUrl({
      name: channel.name,
      category: channel.category,
      channelUrl,
      liveUrl,
      enabled: true,
    });

    imported += 1;
  }

  return {
    imported,
    categories: defaultYouTubeLiveCatalog.categories.length,
    channels: defaultYouTubeLiveCatalog.channels.length,
  };
}
