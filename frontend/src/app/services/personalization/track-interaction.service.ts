import {Injectable} from '@angular/core';
import {EMPTY, catchError} from 'rxjs';
import {
  TrackInteractionContext,
  TrackInteractionEndpointService,
  TrackInteractionType
} from '../../endpoints/personalization-endpoints/track-interaction-endpoint.service';

export interface RecordInteractionOptions {
  playedMs?: number;
  trackDurationMs?: number;
  contextType?: TrackInteractionContext;
}

@Injectable({providedIn: 'root'})
export class TrackInteractionService {
  constructor(private endpoint: TrackInteractionEndpointService) {}

  record(
    trackId: number,
    interactionType: TrackInteractionType,
    options: RecordInteractionOptions = {}
  ): void {
    if (trackId <= 0) {
      return;
    }

    const clientEventId = this.createEventId();
    this.endpoint.handleAsync({
      trackId,
      interactionType,
      playedMs: options.playedMs,
      trackDurationMs: options.trackDurationMs,
      contextType: options.contextType,
      clientEventId,
      occurredAt: new Date().toISOString()
    }).pipe(
      catchError(error => {
        console.warn(`Could not record ${interactionType} interaction.`, error);
        return EMPTY;
      })
    ).subscribe();
  }

  private createEventId(): string {
    return typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
      ? crypto.randomUUID()
      : `${Date.now()}-${Math.random().toString(16).slice(2)}`;
  }
}
