import {HttpErrorResponse} from '@angular/common/http';
import {
  Component,
  ElementRef,
  OnInit,
  QueryList,
  ViewChildren
} from '@angular/core';
import {
  AbstractControl,
  UntypedFormArray,
  UntypedFormBuilder,
  UntypedFormGroup,
  Validators
} from '@angular/forms';
import {ActivatedRoute, Router} from '@angular/router';
import {MatSnackBar} from '@angular/material/snack-bar';
import {
  AdminPlaylistThemeLabel,
  AdminPlaylistThemeTagNamespace,
  AdminPlaylistThemesEndpointService,
  CreateAdminPlaylistThemeRequest,
  PlaylistThemeLabelPolarity,
  PlaylistThemeLabelSource,
  UpdateAdminPlaylistThemeRequest
} from '../../../../endpoints/admin-endpoints/admin-playlist-themes-endpoint.service';

type LabelPolarityFilter = 'All' | PlaylistThemeLabelPolarity;

@Component({
  selector: 'app-playlist-theme-editor',
  templateUrl: './playlist-theme-editor.component.html',
  styleUrl: './playlist-theme-editor.component.css'
})
export class PlaylistThemeEditorComponent implements OnInit {
  @ViewChildren('labelInput')
  private labelInputs!: QueryList<ElementRef<HTMLInputElement>>;

  readonly themeId: string | null;
  readonly isEditMode: boolean;
  readonly form: UntypedFormGroup;
  loading = false;
  saving = false;
  errorMessage = '';
  tagCatalogLoading = true;
  tagCatalogUnavailable = false;
  tagCatalog: AdminPlaylistThemeTagNamespace[] = [];
  labelFilter: LabelPolarityFilter = 'All';

  readonly polarityOptions: PlaylistThemeLabelPolarity[] = ['Positive', 'Negative'];
  readonly sourceOptions: PlaylistThemeLabelSource[] = ['EssentiaTag', 'ClapText'];
  readonly labelFilters: LabelPolarityFilter[] = ['All', 'Positive', 'Negative'];

  constructor(
    private formBuilder: UntypedFormBuilder,
    private endpoint: AdminPlaylistThemesEndpointService,
    private route: ActivatedRoute,
    private router: Router,
    private snackBar: MatSnackBar
  ) {
    this.themeId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = this.themeId !== null;
    this.form = this.formBuilder.group({
      themeKey: [
        '',
        [
          Validators.required,
          Validators.maxLength(100),
          Validators.pattern(/^[a-z0-9]+(?:-[a-z0-9]+)*$/)
        ]
      ],
      name: ['', [Validators.required, Validators.maxLength(200)]],
      description: ['', Validators.maxLength(500)],
      isActive: [true],
      trackCount: [25, [Validators.required, Validators.min(1), Validators.max(50)]],
      sortOrder: [10, [Validators.required, Validators.min(0), Validators.max(100000)]],
      labels: this.formBuilder.array([])
    });
  }

  ngOnInit(): void {
    this.loadTagCatalog();

    if (this.themeId) {
      this.loadTheme(this.themeId);
    } else {
      this.addLabel(undefined, 'Positive');
    }
  }

  get labels(): UntypedFormArray {
    return this.form.get('labels') as UntypedFormArray;
  }

  get descriptionLength(): number {
    return (this.form.get('description')?.value ?? '').length;
  }

  addLabel(
    label?: AdminPlaylistThemeLabel,
    polarity: PlaylistThemeLabelPolarity = 'Positive'
  ): void {
    this.labels.push(this.createLabelGroup(label, polarity));
  }

  addNewLabel(): void {
    const polarity = this.labelFilter === 'Negative' ? 'Negative' : 'Positive';
    this.labels.insert(0, this.createLabelGroup(undefined, polarity));
    this.form.markAsDirty();

    setTimeout(() => {
      this.labelInputs.first?.nativeElement.focus({preventScroll: true});
    });
  }

  private createLabelGroup(
    label?: AdminPlaylistThemeLabel,
    polarity: PlaylistThemeLabelPolarity = 'Positive'
  ): UntypedFormGroup {
    const group = this.formBuilder.group({
      label: [
        label?.label ?? '',
        [Validators.required, Validators.maxLength(100)]
      ],
      polarity: [label?.polarity ?? polarity, Validators.required],
      source: [label?.source ?? 'EssentiaTag', Validators.required],
      tagNamespace: [label?.tagNamespace ?? this.defaultTagNamespace()],
      weight: [
        label?.weight ?? 1,
        [Validators.required, Validators.min(0.0001), Validators.max(100)]
      ]
    });
    this.syncNamespaceControl(group, false);
    return group;
  }

  setLabelFilter(filter: LabelPolarityFilter): void {
    this.labelFilter = filter;
  }

  labelMatchesFilter(group: AbstractControl): boolean {
    return this.labelFilter === 'All' ||
      group.get('polarity')?.value === this.labelFilter;
  }

  labelCount(filter: LabelPolarityFilter): number {
    return filter === 'All'
      ? this.labels.length
      : this.labels.controls.filter(
        control => control.get('polarity')?.value === filter
      ).length;
  }

  onSourceChanged(group: AbstractControl): void {
    this.syncNamespaceControl(group, true);
  }

  namespaceOptionsFor(group: AbstractControl): string[] {
    if (group.get('source')?.value !== 'EssentiaTag') {
      return [];
    }

    const selected = group.get('tagNamespace')?.value?.trim();
    const namespaces = this.tagCatalog.map(item => item.namespace);
    if (selected && !namespaces.some(item =>
      item.toLowerCase() === selected.toLowerCase()
    )) {
      namespaces.push(selected);
    }

    return namespaces.sort((left, right) =>
      left.localeCompare(right, undefined, {sensitivity: 'base'})
    );
  }

  tagSuggestionsFor(group: AbstractControl): string[] {
    if (group.get('source')?.value !== 'EssentiaTag') {
      return [];
    }

    const selected = group.get('tagNamespace')?.value?.trim();
    return this.tagCatalog.find(item =>
      item.namespace.toLowerCase() === selected?.toLowerCase()
    )?.labels ?? [];
  }

  filteredTagSuggestionsFor(group: AbstractControl): string[] {
    const query = String(group.get('label')?.value ?? '')
      .trim()
      .toLocaleLowerCase();
    const suggestions = this.tagSuggestionsFor(group);

    return (query
      ? suggestions.filter(tag => tag.toLocaleLowerCase().includes(query))
      : suggestions
    ).slice(0, 40);
  }

  namespaceLabel(tagNamespace: string): string {
    return tagNamespace
      .replace(/^discogs\./i, 'Discogs · ')
      .replace(/[._-]+/g, ' ')
      .replace(/\b\w/g, character => character.toUpperCase());
  }

  removeLabel(index: number): void {
    if (this.labels.length > 1) {
      this.labels.removeAt(index);
      this.form.markAsDirty();
    }
  }

  sourceDescription(group: AbstractControl): string {
    if (group.get('source')?.value === 'ClapText') {
      return 'Stored for future CLAP scoring';
    }

    const namespace = group.get('tagNamespace')?.value;
    if (!namespace) {
      return this.tagCatalogLoading
        ? 'Loading analyzed tag namespaces…'
        : 'Select a namespace to see matching tags';
    }

    const count = this.tagSuggestionsFor(group).length;
    return `${count} ${count === 1 ? 'tag' : 'tags'} available in this namespace`;
  }

  save(): void {
    this.errorMessage = '';
    this.form.markAllAsTouched();

    if (this.form.invalid) {
      this.errorMessage = 'Review the highlighted fields before saving.';
      return;
    }

    const raw = this.form.getRawValue();
    const labels = raw.labels as AdminPlaylistThemeLabel[];
    const hasPositiveEssentia = labels.some(label =>
      label.polarity === 'Positive' && label.source === 'EssentiaTag'
    );
    if (!hasPositiveEssentia) {
      this.errorMessage = 'Add at least one positive Essentia tag so this theme can influence recommendations.';
      return;
    }

    this.saving = true;
    const updateRequest: UpdateAdminPlaylistThemeRequest = {
      name: raw.name.trim(),
      description: raw.description.trim(),
      isActive: raw.isActive,
      trackCount: Number(raw.trackCount),
      sortOrder: Number(raw.sortOrder),
      labels: labels.map(label => ({
        label: label.label.trim(),
        polarity: label.polarity,
        source: label.source,
        tagNamespace: label.source === 'EssentiaTag'
          ? label.tagNamespace?.trim() ?? null
          : null,
        weight: Number(label.weight)
      }))
    };

    const request$ = this.themeId
      ? this.endpoint.update(this.themeId, updateRequest)
      : this.endpoint.create({
          themeKey: raw.themeKey.trim().toLowerCase(),
          ...updateRequest
        } as CreateAdminPlaylistThemeRequest);

    request$.subscribe({
      next: theme => {
        this.saving = false;
        this.form.markAsPristine();
        this.snackBar.open(
          `${theme.name} was ${this.isEditMode ? 'updated' : 'created'}.`,
          'Dismiss',
          {duration: 4000}
        );
        this.router.navigate(['/admin/playlist-themes']);
      },
      error: (error: HttpErrorResponse) => {
        this.saving = false;
        this.errorMessage =
          error.error?.detail ??
          error.error?.title ??
          'The playlist theme could not be saved.';
      }
    });
  }

  cancel(): void {
    if (!this.form.dirty || window.confirm('Discard your unsaved theme changes?')) {
      this.router.navigate(['/admin/playlist-themes']);
    }
  }

  controlHasError(controlName: string, errorName?: string): boolean {
    const control = this.form.get(controlName);
    return !!control && control.touched &&
      (errorName ? control.hasError(errorName) : control.invalid);
  }

  private loadTheme(id: string): void {
    this.loading = true;
    this.endpoint.get(id).subscribe({
      next: theme => {
        this.form.patchValue({
          themeKey: theme.themeKey,
          name: theme.name,
          description: theme.description,
          isActive: theme.isActive,
          trackCount: theme.trackCount,
          sortOrder: theme.sortOrder
        });
        this.form.get('themeKey')?.disable();
        this.labels.clear();
        theme.labels.forEach(label => this.addLabel(label));
        if (theme.labels.length === 0) {
          this.addLabel(undefined, 'Positive');
        }
        this.form.markAsPristine();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'This playlist theme could not be loaded.';
      }
    });
  }

  private loadTagCatalog(): void {
    this.endpoint.getTagCatalog().subscribe({
      next: catalog => {
        this.tagCatalog = catalog;
        this.tagCatalogLoading = false;
        this.applyDefaultNamespaceToEmptyLabels();
      },
      error: () => {
        this.tagCatalogLoading = false;
        this.tagCatalogUnavailable = true;
      }
    });
  }

  private applyDefaultNamespaceToEmptyLabels(): void {
    const defaultNamespace = this.defaultTagNamespace();
    if (!defaultNamespace) {
      return;
    }

    this.labels.controls.forEach(control => {
      if (control.get('source')?.value === 'EssentiaTag' &&
          !control.get('tagNamespace')?.value) {
        control.get('tagNamespace')?.setValue(defaultNamespace);
        control.get('tagNamespace')?.updateValueAndValidity();
      }
    });
  }

  private defaultTagNamespace(): string {
    return this.tagCatalog[0]?.namespace ?? '';
  }

  private syncNamespaceControl(group: AbstractControl, updateValue: boolean): void {
    const source = group.get('source')?.value as PlaylistThemeLabelSource;
    const namespaceControl = group.get('tagNamespace');
    if (!namespaceControl) {
      return;
    }

    if (source === 'EssentiaTag') {
      namespaceControl.enable({emitEvent: false});
      namespaceControl.setValidators([
        Validators.required,
        Validators.maxLength(50)
      ]);
      if (updateValue && !namespaceControl.value) {
        namespaceControl.setValue(this.defaultTagNamespace());
      }
    } else {
      namespaceControl.clearValidators();
      if (updateValue) {
        namespaceControl.setValue(null);
      }
      namespaceControl.disable({emitEvent: false});
    }

    namespaceControl.updateValueAndValidity({emitEvent: false});
  }
}
