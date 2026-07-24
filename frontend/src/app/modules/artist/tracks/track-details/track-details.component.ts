import {Component, Inject, OnDestroy, OnInit} from '@angular/core';
import {FormControl, FormGroup, Validators} from '@angular/forms';
import {ActivatedRoute, Router} from '@angular/router';
import {MAT_DIALOG_DATA, MatDialog, MatDialogRef} from '@angular/material/dialog';
import {MatSnackBar} from '@angular/material/snack-bar';
import {MatSlideToggleChange} from '@angular/material/slide-toggle';
import {Subscription, debounceTime, distinctUntilChanged, of, switchMap, timer} from 'rxjs';
import {ArtistHandlerService} from '../../../../services/artist-handler.service';
import {MyConfig} from '../../../../my-config';
import {
  ArtistSearchV2,
  ReleaseSearchV2,
  TrackArtistV2,
  TrackDetailsV2,
  TrackManagementV2EndpointService,
  TrackReleaseV2,
  TrackStemSetV2
} from '../../../../endpoints/track-endpoints/track-management-v2-endpoint.service';
import {TrackPlaybackManifestEndpointService} from '../../../../endpoints/track-endpoints/track-playback-manifest-endpoint.service';
import {ConfirmDialogComponent} from '../../../shared/dialogs/confirm-dialog/confirm-dialog.component';
import {HttpEventType} from '@angular/common/http';

@Component({
  selector: 'app-featured-artist-settings-dialog',
  template: `
    <h2 mat-dialog-title>Featured artist settings</h2>
    <mat-dialog-content>
      <p><strong>{{ data.name }}</strong></p>
      <p class="dialog-note">This relationship is always a featured credit; lead status cannot be changed here.</p>
      <mat-slide-toggle [checked]="false" disabled>Lead artist</mat-slide-toggle>
      <mat-slide-toggle [formControl]="showOnProfile">Show this track on the artist's profile</mat-slide-toggle>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="dialogRef.close()">Cancel</button>
      <button mat-flat-button (click)="save()">Apply</button>
    </mat-dialog-actions>
  `,
  styles: ['.dialog-note{color:#a99ea5;margin-bottom:20px;max-width:420px}']
})
export class FeaturedArtistSettingsDialogComponent {
  readonly showOnProfile: FormControl<boolean>;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: TrackArtistV2,
    public dialogRef: MatDialogRef<FeaturedArtistSettingsDialogComponent>
  ) {
    this.showOnProfile = new FormControl(this.data.showOnProfile, {nonNullable: true});
  }

  save(): void {
    this.dialogRef.close({...this.data, showOnProfile: this.showOnProfile.value});
  }
}

@Component({
  selector: 'app-track-release-settings-dialog',
  template: `
    <h2 mat-dialog-title>{{ data.release.associationId === undefined ? 'Add to release' : 'Release settings' }}</h2>
    <mat-dialog-content>
      <h3>{{ data.release.title }}</h3>
      <form [formGroup]="form" class="release-dialog-form">
        <mat-form-field appearance="outline"><mat-label>Disc</mat-label><input matInput type="number" min="1" formControlName="discNumber"></mat-form-field>
        <mat-form-field appearance="outline"><mat-label>Track number</mat-label><input matInput type="number" min="1" formControlName="trackNumber"></mat-form-field>
        <mat-form-field appearance="outline" class="wide"><mat-label>Title override</mat-label><input matInput maxlength="200" formControlName="titleOverride"></mat-form-field>
        <mat-slide-toggle formControlName="isPrimaryRelease" class="wide">Primary release</mat-slide-toggle>
      </form>
    </mat-dialog-content>
    <mat-dialog-actions align="end">
      <button mat-button (click)="dialogRef.close()">Cancel</button>
      <button mat-flat-button [disabled]="form.invalid" (click)="save()">Apply</button>
    </mat-dialog-actions>
  `,
  styles: ['.release-dialog-form{display:grid;grid-template-columns:1fr 1fr;gap:12px;min-width:min(520px,75vw)}.wide{grid-column:1/-1}h3{margin-top:0}']
})
export class TrackReleaseSettingsDialogComponent {
  readonly form: FormGroup;

  constructor(
    @Inject(MAT_DIALOG_DATA) public data: {release: Partial<TrackReleaseV2> & {title: string}; nextTrackNumber: number},
    public dialogRef: MatDialogRef<TrackReleaseSettingsDialogComponent>
  ) {
    this.form = new FormGroup({
      discNumber: new FormControl(this.data.release.discNumber ?? 1, {nonNullable: true, validators: [Validators.required, Validators.min(1)]}),
      trackNumber: new FormControl(this.data.release.trackNumber ?? this.data.nextTrackNumber, {nonNullable: true, validators: [Validators.required, Validators.min(1)]}),
      titleOverride: new FormControl(this.data.release.titleOverride ?? ''),
      isPrimaryRelease: new FormControl(this.data.release.isPrimaryRelease ?? false, {nonNullable: true})
    });
  }

  save(): void {
    if (this.form.invalid) return;
    this.dialogRef.close({
      ...this.data.release,
      ...this.form.getRawValue(),
      titleOverride: this.form.controls['titleOverride'].value?.trim() || null
    });
  }
}

export interface StemUploadDialogResult {
  stemProfile: string;
  files: Partial<Record<'vocals' | 'drums' | 'bass' | 'other' | 'instrumental', File>>;
}

@Component({
  selector: 'app-stem-set-upload-dialog',
  template: `
    <h2 mat-dialog-title>Replace stem set</h2>
    <mat-dialog-content>
      <mat-form-field appearance="outline" class="full"><mat-label>Stem profile</mat-label>
        <mat-select [formControl]="profile"><mat-option value="four-stem">Four stem</mat-option><mat-option value="two-stem-vocals">Vocals + instrumental</mat-option></mat-select>
      </mat-form-field>
      <div class="file-grid">
        <label>Vocals<input type="file" accept="audio/*" (change)="select('vocals', $event)"></label>
        @if (profile.value === 'four-stem') {
          <label>Drums<input type="file" accept="audio/*" (change)="select('drums', $event)"></label>
          <label>Bass<input type="file" accept="audio/*" (change)="select('bass', $event)"></label>
          <label>Other<input type="file" accept="audio/*" (change)="select('other', $event)"></label>
        } @else {
          <label>Instrumental<input type="file" accept="audio/*" (change)="select('instrumental', $event)"></label>
        }
      </div>
    </mat-dialog-content>
    <mat-dialog-actions align="end"><button mat-button (click)="dialogRef.close()">Cancel</button><button mat-flat-button [disabled]="!valid" (click)="save()">Upload set</button></mat-dialog-actions>
  `,
  styles: ['.full{width:100%}.file-grid{display:grid;gap:12px;min-width:min(520px,75vw)}label{display:flex;align-items:center;justify-content:space-between;gap:16px;padding:12px;border-radius:12px;background:#242424}']
})
export class StemSetUploadDialogComponent {
  readonly profile = new FormControl('four-stem', {nonNullable: true});
  readonly files: StemUploadDialogResult['files'] = {};

  constructor(public dialogRef: MatDialogRef<StemSetUploadDialogComponent>) {}

  get valid(): boolean {
    return this.profile.value === 'four-stem'
      ? !!(this.files.vocals && this.files.drums && this.files.bass && this.files.other)
      : !!(this.files.vocals && this.files.instrumental);
  }

  select(name: keyof StemUploadDialogResult['files'], event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (file) this.files[name] = file;
  }

  save(): void {
    if (this.valid) this.dialogRef.close({stemProfile: this.profile.value, files: this.files});
  }
}

@Component({
  selector: 'app-track-details',
  templateUrl: './track-details.component.html',
  styleUrl: './track-details.component.css'
})
export class TrackDetailsComponent implements OnInit, OnDestroy {
  readonly metadataForm = new FormGroup({
    title: new FormControl('', {nonNullable: true, validators: [Validators.required, Validators.minLength(3), Validators.maxLength(200)]}),
    isExplicit: new FormControl(false, {nonNullable: true})
  });
  readonly artistSearch = new FormControl('', {nonNullable: true});
  readonly releaseSearch = new FormControl('', {nonNullable: true});

  details: TrackDetailsV2 | null = null;
  featuredArtists: TrackArtistV2[] = [];
  releases: TrackReleaseV2[] = [];
  artistResults: ArtistSearchV2[] = [];
  releaseResults: ReleaseSearchV2[] = [];
  stemSets: TrackStemSetV2[] = [];
  masterUrl = '';
  masterFile: File | null = null;
  masterProgress: number | null = null;
  loading = true;
  errorMessage = '';
  savingSection = '';
  trackId = 0;

  private originalFeaturedArtists: TrackArtistV2[] = [];
  private originalReleases: TrackReleaseV2[] = [];
  private readonly subscriptions = new Subscription();
  private stemPolling?: Subscription;
  private lastMasterUrlRefreshAt = 0;
  private lastStemUrlRefreshAt = 0;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private endpoint: TrackManagementV2EndpointService,
    private playbackEndpoint: TrackPlaybackManifestEndpointService,
    private artistHandler: ArtistHandlerService,
    private dialog: MatDialog,
    private snackBar: MatSnackBar
  ) {}

  ngOnInit(): void {
    this.trackId = Number(this.route.snapshot.paramMap.get('trackId'));
    this.loadDetails();
    this.loadPlayback();
    this.startStemPolling();

    this.subscriptions.add(this.artistSearch.valueChanges.pipe(
      debounceTime(250), distinctUntilChanged(),
      switchMap(query => this.details && query.trim()
        ? this.endpoint.searchArtists(query.trim(), this.details.leadArtist.artistId)
        : of([]))
    ).subscribe(results => this.artistResults = results.filter(x => !this.featuredArtists.some(a => a.artistId === x.id))));

    this.subscriptions.add(this.releaseSearch.valueChanges.pipe(
      debounceTime(250), distinctUntilChanged(),
      switchMap(query => this.details && query.trim()
        ? this.endpoint.searchReleases(this.details.leadArtist.artistId, query.trim(), this.trackId)
        : of({items: [] as ReleaseSearchV2[]} as any))
    ).subscribe(response => this.releaseResults = (response.items ?? [])
      .filter((candidate: ReleaseSearchV2) => !this.releases.some(release => release.releaseId === candidate.id))));
  }

  ngOnDestroy(): void {
    this.subscriptions.unsubscribe();
    this.stemPolling?.unsubscribe();
  }

  get canEdit(): boolean {
    return ['Owner', 'General Manager', 'Streaming Manager', 'Admin'].includes(this.artistHandler.getSelectedArtist()?.role ?? '');
  }

  get activeStemSet(): TrackStemSetV2 | null {
    return this.stemSets.find(x => x.isActive) ?? null;
  }

  get latestManualStemSet(): TrackStemSetV2 | null {
    return this.stemSets.find(x => x.source === 'ArtistUploaded' && x.status === 'Ready') ?? null;
  }

  get latestAiStemSet(): TrackStemSetV2 | null {
    return this.stemSets.find(x => x.source === 'AiGenerated' && x.status === 'Ready') ?? null;
  }

  get isAiActive(): boolean {
    return this.activeStemSet?.source === 'AiGenerated';
  }

  get estimatedHours(): number {
    return ((this.details?.lengthSeconds ?? 0) * (this.details?.streams ?? 0)) / 3600;
  }

  loadDetails(): void {
    this.loading = true;
    this.endpoint.getDetails(this.trackId).subscribe({
      next: details => {
        this.details = details;
        this.metadataForm.setValue({title: details.title, isExplicit: details.isExplicit});
        this.originalFeaturedArtists = this.clone(details.featuredArtists);
        this.originalReleases = this.clone(details.releases);
        this.featuredArtists = this.clone(details.featuredArtists);
        this.releases = this.clone(details.releases);
        this.loading = false;
      },
      error: error => {
        this.loading = false;
        this.errorMessage = error?.error?.message ?? error?.error ?? 'Track details could not be loaded.';
      }
    });
  }

  loadPlayback(): void {
    this.playbackEndpoint.handleAsync({trackId: this.trackId, artistMode: true}).subscribe({
      next: response => this.masterUrl = response.stream.master.url,
      error: () => this.masterUrl = ''
    });
  }

  refreshMasterPreview(): void {
    const now = Date.now();
    if (now - this.lastMasterUrlRefreshAt < 5000) return;
    this.lastMasterUrlRefreshAt = now;
    this.loadPlayback();
  }

  saveMetadata(): void {
    if (this.metadataForm.invalid || !this.canEdit) return;
    this.savingSection = 'metadata';
    this.endpoint.updateMetadata(
      this.trackId,
      this.metadataForm.controls.title.value.trim(),
      this.metadataForm.controls.isExplicit.value
    ).subscribe({next: () => this.finishSave('Track information saved.'), error: error => this.failSave(error)});
  }

  cancelMetadata(): void {
    if (this.details) this.metadataForm.setValue({title: this.details.title, isExplicit: this.details.isExplicit});
  }

  setMasterFile(file: File | undefined): void {
    this.masterFile = file ?? null;
  }

  replaceMaster(): void {
    if (!this.masterFile || !this.canEdit) return;
    this.savingSection = 'master';
    this.masterProgress = 0;
    this.endpoint.replaceMaster(this.trackId, this.masterFile).subscribe({
      next: event => {
        if (event.type === HttpEventType.UploadProgress) {
          this.masterProgress = event.total ? Math.round(100 * event.loaded / event.total) : null;
          return;
        }
        if (event.type !== HttpEventType.Response) return;
        this.masterFile = null;
        this.masterProgress = null;
        this.loadPlayback();
        this.startStemPolling();
        this.finishSave('Master replaced. Analysis and stems are processing.');
      },
      error: error => {
        this.masterProgress = null;
        this.failSave(error);
      }
    });
  }

  addFeaturedArtist(artist: ArtistSearchV2): void {
    if (this.featuredArtists.some(x => x.artistId === artist.id)) return;
    this.featuredArtists.push({
      artistTrackId: 0,
      artistId: artist.id,
      name: artist.name,
      profilePhotoPath: artist.profilePhotoPath,
      isLead: false,
      showOnProfile: true
    });
    this.artistSearch.setValue('');
  }

  editFeaturedArtist(artist: TrackArtistV2): void {
    this.dialog.open(FeaturedArtistSettingsDialogComponent, {data: {...artist}}).afterClosed().subscribe(result => {
      if (!result) return;
      const index = this.featuredArtists.findIndex(x => x.artistId === artist.artistId);
      if (index >= 0) this.featuredArtists[index] = result;
    });
  }

  removeFeaturedArtist(artist: TrackArtistV2): void {
    this.featuredArtists = this.featuredArtists.filter(x => x.artistId !== artist.artistId);
  }

  saveFeaturedArtists(): void {
    this.savingSection = 'artists';
    this.endpoint.replaceFeaturedArtists(this.trackId, this.featuredArtists.map(x => ({
      artistId: x.artistId,
      showOnProfile: x.showOnProfile
    }))).subscribe({
      next: artists => {
        this.featuredArtists = this.clone(artists);
        this.originalFeaturedArtists = this.clone(artists);
        this.finishSave('Featured artists saved.', false);
      },
      error: error => this.failSave(error)
    });
  }

  cancelFeaturedArtists(): void {
    this.featuredArtists = this.clone(this.originalFeaturedArtists);
  }

  addRelease(release: ReleaseSearchV2): void {
    const draft: Partial<TrackReleaseV2> & {title: string} = {
      releaseId: release.id,
      title: release.title,
      coverPath: release.coverPath,
      releaseDate: release.releaseDate,
      releaseType: release.releaseType,
      isPrimaryRelease: this.releases.length === 0
    };
    this.openReleaseDialog(draft, true);
  }

  editRelease(release: TrackReleaseV2): void {
    this.openReleaseDialog({...release}, false);
  }

  removeRelease(release: TrackReleaseV2): void {
    this.releases = this.releases.filter(x => x.releaseId !== release.releaseId);
  }

  saveReleases(): void {
    this.savingSection = 'releases';
    this.endpoint.replaceReleases(this.trackId, this.releases.map(x => ({
      releaseId: x.releaseId,
      discNumber: x.discNumber,
      trackNumber: x.trackNumber,
      titleOverride: x.titleOverride,
      isPrimaryRelease: x.isPrimaryRelease
    }))).subscribe({
      next: releases => {
        this.releases = this.clone(releases);
        this.originalReleases = this.clone(releases);
        this.finishSave('Associated releases saved.', false);
      },
      error: error => this.failSave(error)
    });
  }

  cancelReleases(): void {
    this.releases = this.clone(this.originalReleases);
  }

  changeAiSource(event: MatSlideToggleChange): void {
    if (event.checked) {
      if (this.latestAiStemSet) this.activateStemSet(this.latestAiStemSet);
      else {
        this.endpoint.separateStems(this.trackId).subscribe({
          next: () => {
            this.snackBar.open('AI stem separation queued.', 'Dismiss', {duration: 3000});
            this.startStemPolling();
          },
          error: error => this.failSave(error)
        });
      }
    } else if (this.latestManualStemSet) {
      this.activateStemSet(this.latestManualStemSet);
    }
  }

  activateStemSet(set: TrackStemSetV2): void {
    this.endpoint.activateStemSet(this.trackId, set.id).subscribe({
      next: () => this.endpoint.getStems(this.trackId).subscribe(result => this.stemSets = result.stemSets),
      error: error => this.failSave(error)
    });
  }

  refreshStemUrls(): void {
    const now = Date.now();
    if (now - this.lastStemUrlRefreshAt < 5000) return;
    this.lastStemUrlRefreshAt = now;
    this.endpoint.getStems(this.trackId).subscribe({
      next: response => this.stemSets = response.stemSets
    });
  }

  openStemUpload(): void {
    this.dialog.open(StemSetUploadDialogComponent).afterClosed().subscribe((result: StemUploadDialogResult | undefined) => {
      if (!result) return;
      this.savingSection = 'stems';
      this.endpoint.uploadStemSet(this.trackId, result.stemProfile, result.files).subscribe({
        next: () => {
          this.startStemPolling();
          this.finishSave('Manual stem set uploaded.', false);
        },
        error: error => this.failSave(error)
      });
    });
  }

  deleteStemSet(set: TrackStemSetV2): void {
    this.dialog.open(ConfirmDialogComponent, {data: {
      title: 'Delete this stem set?',
      content: 'The stored stem files will be removed. Another ready set will become active when available.'
    }}).afterClosed().subscribe(confirmed => {
      if (!confirmed) return;
      this.endpoint.deleteStemSet(this.trackId, set.id).subscribe({
        next: () => this.endpoint.getStems(this.trackId).subscribe(response => this.stemSets = response.stemSets),
        error: error => this.failSave(error)
      });
    });
  }

  openStatistics(): void {
    this.router.navigate(['/artist/tracks', this.trackId, 'statistics']);
  }

  goBack(): void {
    this.router.navigate(['/artist/tracks']);
  }

  mediaUrl(path: string, folder: 'ArtistPfps' | 'AlbumCovers'): string {
    if (!path) return `${MyConfig.api_address}/media/Images/playlist_placeholder.png`;
    if (/^https?:/.test(path)) return path;
    if (path.startsWith('/')) return `${MyConfig.api_address}${path}`;
    return `${MyConfig.api_address}/media/Images/${folder}/${path}`;
  }

  private openReleaseDialog(release: Partial<TrackReleaseV2> & {title: string}, isNew: boolean): void {
    this.dialog.open(TrackReleaseSettingsDialogComponent, {
      data: {release: isNew ? {...release, associationId: undefined} : release, nextTrackNumber: this.releases.length + 1}
    }).afterClosed().subscribe((result: TrackReleaseV2 | undefined) => {
      if (!result) return;
      const normalized: TrackReleaseV2 = {
        associationId: result.associationId ?? null,
        releaseId: result.releaseId,
        title: result.title,
        coverPath: result.coverPath ?? '',
        releaseDate: result.releaseDate ?? new Date().toISOString(),
        releaseType: result.releaseType ?? 'Release',
        discNumber: result.discNumber,
        trackNumber: result.trackNumber,
        titleOverride: result.titleOverride,
        isPrimaryRelease: result.isPrimaryRelease,
        isLegacyAssociation: result.isLegacyAssociation ?? false
      };
      if (normalized.isPrimaryRelease) this.releases.forEach(x => x.isPrimaryRelease = false);
      const index = this.releases.findIndex(x => x.releaseId === normalized.releaseId);
      if (index >= 0) this.releases[index] = normalized;
      else this.releases.push(normalized);
      this.releaseSearch.setValue('');
    });
  }

  private finishSave(message: string, reload = true): void {
    this.savingSection = '';
    this.snackBar.open(message, 'Dismiss', {duration: 3000});
    if (reload) this.loadDetails();
  }

  private failSave(error: any): void {
    this.savingSection = '';
    this.snackBar.open(error?.error?.message ?? error?.error ?? 'The operation failed.', 'Dismiss', {duration: 4500});
  }

  private startStemPolling(): void {
    this.stemPolling?.unsubscribe();
    this.stemPolling = timer(0, 5000).pipe(
      switchMap(() => this.endpoint.getStems(this.trackId))
    ).subscribe({
      next: response => {
        this.stemSets = response.stemSets;
        if (response.stemSets.every(set => set.status !== 'Pending' && set.status !== 'Processing')) {
          this.stemPolling?.unsubscribe();
        }
      },
      error: () => this.stemPolling?.unsubscribe()
    });
  }

  private clone<T>(value: T): T {
    return JSON.parse(JSON.stringify(value));
  }
}
