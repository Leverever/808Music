import {ElementRef} from '@angular/core';
import {fakeAsync, flushMicrotasks} from '@angular/core/testing';
import {BehaviorSubject, Subject} from 'rxjs';
import {MusicControllerComponent} from './music-controller.component';
import {TrackGetResponse} from '../../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';

describe('MusicControllerComponent playback ownership', () => {
  const track: TrackGetResponse = {
    id: 23,
    title: 'Stem test',
    length: 200,
    streams: 0,
    isExplicit: false,
    coverPath: '/cover.jpg',
    trackUserInfo: [],
    artists: [],
    albumId: null
  };

  it('cancels a pending master play when playback is paused or its mode changes', fakeAsync(() => {
    const fixture = createComponent();
    const master = createAudioDouble();
    fixture.component.player = master as unknown as HTMLAudioElement;
    fixture.component.masterAudioSource = '/master.mp3';
    fixture.component.playingState = true;

    (fixture.component as any).playActiveAudio();
    (fixture.component as any).pauseAllAudio();
    flushMicrotasks();

    expect(master.play).not.toHaveBeenCalled();
    expect(master.pause).toHaveBeenCalled();
  }));

  it('does not restore the master after its source was intentionally cleared for stems', () => {
    const fixture = createComponent();
    fixture.component.track = track;
    fixture.component.masterAudioSource = '';
    fixture.component.stemModeActive = false;
    (fixture.component as any).usingLegacyMasterStream = false;
    const setMasterSource = spyOn<any>(fixture.component, 'setMasterAudioSource');

    fixture.component.handleMasterPlaybackError();

    expect(setMasterSource).not.toHaveBeenCalled();
  });

  it('unsubscribes from track events when the controller is destroyed', () => {
    const fixture = createComponent();
    (fixture.component as any).playerElement = new ElementRef(document.createElement('audio'));
    fixture.component.ngOnInit();
    fixture.component.ngOnDestroy();

    fixture.trackEvent.next(track);

    expect(fixture.manifestEndpoint.handleAsync).not.toHaveBeenCalled();
  });

  function createComponent() {
    const trackEvent = new Subject<TrackGetResponse>();
    const shuffleToggled = new Subject<boolean>();
    const playStateChange = new BehaviorSubject(false);
    const musicPlayerService = {
      trackEvent,
      shuffleToggled,
      playStateChange,
      isShuffled: false,
      getLastPlayedSong: () => null,
      getPersistedPlaybackTime: () => 0,
      getPlayState: () => false,
      setPlayState: jasmine.createSpy(),
      setPlaybackProgress: jasmine.createSpy(),
      getQueueType: () => 'song',
      playNext: jasmine.createSpy(),
      shufflePlay: jasmine.createSpy(),
      playPrev: jasmine.createSpy(),
      toggleShuffle: jasmine.createSpy()
    };
    const manifestEndpoint = {handleAsync: jasmine.createSpy()};
    const interactionTracker = {
      beginTrack: jasmine.createSpy(),
      playbackStarted: jasmine.createSpy(),
      playbackPaused: jasmine.createSpy(),
      skipTrack: jasmine.createSpy(),
      completeTrack: jasmine.createSpy()
    };
    const component = new MusicControllerComponent(
      musicPlayerService as any,
      {post: jasmine.createSpy()} as any,
      {getAuthToken: () => null} as any,
      manifestEndpoint as any,
      interactionTracker as any);

    return {component, trackEvent, manifestEndpoint};
  }

  function createAudioDouble() {
    return {
      currentTime: 0,
      duration: 200,
      playbackRate: 1,
      pause: jasmine.createSpy(),
      play: jasmine.createSpy().and.returnValue(Promise.resolve())
    };
  }
});
