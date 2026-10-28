import {MusicPlayerService} from './music-player.service';
import {TrackGetResponse} from '../endpoints/track-endpoints/track-get-by-id-endpoint.service';

describe('MusicPlayerService playback state', () => {
  const track: TrackGetResponse = {
    id: 17,
    title: 'First play',
    length: 180,
    streams: 0,
    isExplicit: false,
    coverPath: '/cover.jpg',
    trackUserInfo: [],
    artists: [],
    albumId: null
  };

  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  it('records the intent to play before emitting the first track', () => {
    const service = createService();
    let stateWhenTrackWasEmitted = false;
    service.trackEvent.subscribe(() => stateWhenTrackWasEmitted = service.getPlayState());

    service.createQueue([track]);

    expect(stateWhenTrackWasEmitted).toBeTrue();
    expect(service.getPlayState()).toBeTrue();
  });

  it('does not autoplay a queue restored by a page refresh', () => {
    localStorage.setItem('queue', JSON.stringify({
      queue: [track],
      source: {display: 'Song', value: 'song'},
      type: 'album'
    }));
    localStorage.setItem('playedIndexes', JSON.stringify([0]));

    const service = createService();

    expect(service.getPlayState()).toBeFalse();
  });

  function createService(): MusicPlayerService {
    return new MusicPlayerService({} as any, {} as any, {} as any);
  }
});
