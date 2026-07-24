export interface RecommendationArtistDto {
  artistId: number;
  name: string;
  isLead: boolean;
  profilePhotoPath: string;
}

export interface RecommendationTrackDto {
  trackId: number;
  title: string;
  length: number;
  streams: number;
  isExplicit: boolean;
  albumId: number | null;
  albumTitle?: string | null;
  coverPath: string;
  artists: RecommendationArtistDto[];
  score?: number;
  reason?: string;
  matchedTags?: string[];
  clusterKey?: string | null;
  sourceSignals?: Record<string, number>;
}
