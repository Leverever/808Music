export class MyConfig {
  static api_address = "https://api.marko.software"
  //static api_address = "http://localhost:7000"
  static media_address = this.api_address+"/media/"
  static album_address = this.media_address+"Images/AlbumCovers/"
  //static ui_address = "http://localhost:4200"
  static ui_address = "http://808music.marko.software"

  static mediaUrl(path?: string | null, fallback = "Images/playlist_placeholder.png"): string {
    const value = (path || fallback).trim();

    if (/^(?:https?:|data:|blob:)/i.test(value)) {
      return value;
    }

    const segments = value
      .replace(/^\/+/, '')
      .replace(/^media\/+/i, '')
      .split('/')
      .filter(Boolean);

    if (segments[0]?.toLowerCase() === 'images') {
      segments[0] = 'Images';
    }

    const canonicalImageDirectories: Record<string, string> = {
      albumcovers: 'AlbumCovers',
      artistbgs: 'ArtistBgs',
      artistpfps: 'ArtistPfps',
      playlists: 'Playlists',
      profilepictures: 'ProfilePictures',
      events: 'events',
      logo: 'logo',
      products: 'products'
    };

    if (segments[0] === 'Images' && segments[1]) {
      segments[1] = canonicalImageDirectories[segments[1].toLowerCase()] || segments[1];
    }

    return `${this.api_address}/media/${segments.join('/')}`;
  }
}
