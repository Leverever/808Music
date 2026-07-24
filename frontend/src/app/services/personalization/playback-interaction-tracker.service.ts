import {Injectable} from '@angular/core';
import {TrackInteractionContext} from '../../endpoints/personalization-endpoints/track-interaction-endpoint.service';
import {TrackInteractionService} from './track-interaction.service';

interface PlaybackSession {
  trackId: number;
  durationMs: number;
  contextType: TrackInteractionContext;
  listenedMs: number;
  activeSince: number | null;
  startRecorded: boolean;
}

@Injectable({providedIn: 'root'})
export class PlaybackInteractionTrackerService {
  private session: PlaybackSession | null = null;

  constructor(private interactions: TrackInteractionService) {}

  beginTrack(
    trackId: number,
    durationMs: number,
    contextType: TrackInteractionContext,
    forceNewSession = false
  ): void {
    if (this.session?.trackId === trackId && !forceNewSession) {
      this.session.contextType = contextType;
      this.session.durationMs = Math.max(this.session.durationMs, durationMs);
      return;
    }

    this.finishInterruptedTrack();
    this.session = {
      trackId,
      durationMs: Math.max(0, durationMs),
      contextType,
      listenedMs: 0,
      activeSince: null,
      startRecorded: false
    };
  }

  playbackStarted(trackId?: number): void {
    if (this.session == null || (trackId != null && this.session.trackId !== trackId)) {
      return;
    }

    if (!this.session.startRecorded) {
      this.interactions.record(this.session.trackId, 'PlayStarted', {
        playedMs: 0,
        trackDurationMs: this.session.durationMs,
        contextType: this.session.contextType
      });
      this.session.startRecorded = true;
    }

    if (this.session.activeSince == null) {
      this.session.activeSince = performance.now();
    }
  }

  playbackPaused(): void {
    this.captureActiveListeningTime();
  }

  completeTrack(): void {
    this.finishTrack('PlayCompleted');
  }

  skipTrack(): void {
    if (this.session == null) {
      return;
    }

    this.captureActiveListeningTime();
    const completionRatio = this.session.durationMs > 0
      ? this.session.listenedMs / this.session.durationMs
      : 0;

    this.finishTrack(completionRatio >= 0.9 ? 'PlayCompleted' : 'Skipped');
  }

  private finishInterruptedTrack(): void {
    if (this.session != null && this.session.startRecorded) {
      this.skipTrack();
    } else {
      this.session = null;
    }
  }

  private finishTrack(interactionType: 'PlayCompleted' | 'Skipped'): void {
    if (this.session == null) {
      return;
    }

    this.captureActiveListeningTime();
    const session = this.session;
    this.session = null;

    if (!session.startRecorded) {
      return;
    }

    this.interactions.record(session.trackId, interactionType, {
      playedMs: Math.round(session.listenedMs),
      trackDurationMs: session.durationMs,
      contextType: session.contextType
    });
  }

  private captureActiveListeningTime(): void {
    if (this.session?.activeSince == null) {
      return;
    }

    this.session.listenedMs += Math.max(0, performance.now() - this.session.activeSince);
    this.session.activeSince = null;
  }
}
