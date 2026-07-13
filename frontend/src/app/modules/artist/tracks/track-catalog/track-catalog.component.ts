import {Component, OnDestroy, OnInit} from '@angular/core';
import {FormControl} from '@angular/forms';
import {NavigationEnd, Router} from '@angular/router';
import {PageEvent} from '@angular/material/paginator';
import {Sort} from '@angular/material/sort';
import {Subscription, debounceTime, merge} from 'rxjs';
import {ArtistHandlerService} from '../../../../services/artist-handler.service';
import {
  TrackCatalogItemV2,
  TrackManagementV2EndpointService,
  V2PagedResponse
} from '../../../../endpoints/track-endpoints/track-management-v2-endpoint.service';

@Component({
  selector: 'app-track-catalog',
  templateUrl: './track-catalog.component.html',
  styleUrl: './track-catalog.component.css'
})
export class TrackCatalogComponent implements OnInit, OnDestroy {
  readonly displayedColumns = ['title', 'release', 'duration', 'streams', 'open'];
  readonly searchControl = new FormControl('', {nonNullable: true});
  readonly primaryReleaseControl = new FormControl('', {nonNullable: true});
  readonly minStreamsControl = new FormControl<number | null>(null);
  readonly maxStreamsControl = new FormControl<number | null>(null);
  readonly minDurationMinutesControl = new FormControl<number | null>(null);
  readonly maxDurationMinutesControl = new FormControl<number | null>(null);
  readonly editableRoles = ['Owner', 'General Manager', 'Streaming Manager', 'Admin'];

  tracks: TrackCatalogItemV2[] = [];
  response: V2PagedResponse<TrackCatalogItemV2> | null = null;
  loading = false;
  errorMessage = '';
  creating = false;
  pageNumber = 1;
  pageSize = 20;
  sortBy: 'title' | 'primaryRelease' | 'duration' | 'streams' | undefined;
  sortDirection: 'asc' | 'desc' | undefined;

  private readonly subscriptions = new Subscription();

  constructor(
    private endpoint: TrackManagementV2EndpointService,
    private artistHandler: ArtistHandlerService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.creating = this.router.url.endsWith('/create');
    this.subscriptions.add(this.router.events.subscribe(event => {
      if (event instanceof NavigationEnd) {
        const wasCreating = this.creating;
        this.creating = event.urlAfterRedirects.endsWith('/create');
        if (wasCreating && !this.creating) this.load();
      }
    }));
    this.subscriptions.add(merge(
      this.minStreamsControl.valueChanges,
      this.maxStreamsControl.valueChanges,
      this.minDurationMinutesControl.valueChanges,
      this.maxDurationMinutesControl.valueChanges
    ).pipe(debounceTime(350)).subscribe(() => this.applyFilters()));
    this.load();
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
  }

  get canEdit(): boolean {
    const role = this.artistHandler.getSelectedArtist()?.role ?? '';
    return this.editableRoles.includes(role);
  }

  get rangeFiltersValid(): boolean {
    return this.areNonNegative([
      this.minStreamsControl.value,
      this.maxStreamsControl.value,
      this.minDurationMinutesControl.value,
      this.maxDurationMinutesControl.value
    ]) &&
      this.isValidRange(this.minStreamsControl.value, this.maxStreamsControl.value) &&
      this.isValidRange(this.minDurationMinutesControl.value, this.maxDurationMinutesControl.value);
  }

  get hasActiveFilters(): boolean {
    return !!(
      this.searchControl.value.trim() ||
      this.primaryReleaseControl.value.trim() ||
      this.minStreamsControl.value !== null ||
      this.maxStreamsControl.value !== null ||
      this.minDurationMinutesControl.value !== null ||
      this.maxDurationMinutesControl.value !== null
    );
  }

  load(): void {
    const artist = this.artistHandler.getSelectedArtist();
    if (!artist) {
      this.errorMessage = 'Select an artist profile to view tracks.';
      return;
    }

    this.loading = true;
    this.errorMessage = '';
    this.endpoint.listArtistTracks(artist.id, {
      pageNumber: this.pageNumber,
      pageSize: this.pageSize,
      title: this.searchControl.value.trim() || undefined,
      primaryReleaseTitle: this.primaryReleaseControl.value.trim() || undefined,
      minStreams: this.nonNegativeValue(this.minStreamsControl.value),
      maxStreams: this.nonNegativeValue(this.maxStreamsControl.value),
      minDurationSeconds: this.minutesToSeconds(this.minDurationMinutesControl.value),
      maxDurationSeconds: this.minutesToSeconds(this.maxDurationMinutesControl.value),
      sortBy: this.sortBy,
      sortDirection: this.sortDirection
    }).subscribe({
      next: response => {
        this.response = response;
        this.tracks = response.items;
        this.loading = false;
      },
      error: error => {
        this.loading = false;
        this.errorMessage = error?.error?.message ?? error?.error ?? 'Tracks could not be loaded.';
      }
    });
  }

  changePage(event: PageEvent): void {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.load();
  }

  changeSort(sort: Sort): void {
    if (!sort.direction) {
      this.sortBy = undefined;
      this.sortDirection = undefined;
    } else {
      this.sortBy = sort.active as 'title' | 'primaryRelease' | 'duration' | 'streams';
      this.sortDirection = sort.direction;
    }

    this.pageNumber = 1;
    this.load();
  }

  filterTitle(value: string): void {
    this.searchControl.setValue(value, {emitEvent: false});
    this.applyFilters();
  }

  filterPrimaryRelease(value: string): void {
    this.primaryReleaseControl.setValue(value, {emitEvent: false});
    this.applyFilters();
  }

  openTrack(track: TrackCatalogItemV2): void {
    this.router.navigate(['/artist/tracks', track.id]);
  }

  openCreate(): void {
    this.router.navigate(['/artist/tracks/create']);
  }

  formatDuration(seconds: number): string {
    const minutes = Math.floor(seconds / 60);
    return `${minutes}:${(seconds % 60).toString().padStart(2, '0')}`;
  }

  featuredArtistNames(track: TrackCatalogItemV2): string {
    return track.featuredArtists.length
      ? `feat. ${track.featuredArtists.map(artist => artist.name).join(', ')}`
      : 'Solo track';
  }

  private applyFilters(): void {
    if (!this.rangeFiltersValid) return;
    this.pageNumber = 1;
    this.load();
  }

  private isValidRange(minimum: number | null, maximum: number | null): boolean {
    return minimum === null || maximum === null || minimum <= maximum;
  }

  private areNonNegative(values: (number | null)[]): boolean {
    return values.every(value => value === null || value >= 0);
  }

  private nonNegativeValue(value: number | null): number | undefined {
    return value === null || value < 0 ? undefined : Math.round(value);
  }

  private minutesToSeconds(value: number | null): number | undefined {
    return value === null || value < 0 ? undefined : Math.round(value * 60);
  }
}
