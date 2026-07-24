import {Component, OnInit} from '@angular/core';
import {MatSnackBar} from '@angular/material/snack-bar';
import {
  AdminPlaylistTheme,
  AdminPlaylistThemesEndpointService
} from '../../../endpoints/admin-endpoints/admin-playlist-themes-endpoint.service';

type ThemeStateFilter = 'all' | 'active' | 'inactive';

@Component({
  selector: 'app-playlist-themes',
  templateUrl: './playlist-themes.component.html',
  styleUrl: './playlist-themes.component.css'
})
export class PlaylistThemesComponent implements OnInit {
  themes: AdminPlaylistTheme[] = [];
  loading = true;
  errorMessage = '';
  search = '';
  stateFilter: ThemeStateFilter = 'all';
  readonly updating = new Set<string>();

  constructor(
    private endpoint: AdminPlaylistThemesEndpointService,
    private snackBar: MatSnackBar
  ) {
  }

  ngOnInit(): void {
    this.load();
  }

  get filteredThemes(): AdminPlaylistTheme[] {
    const search = this.search.trim().toLowerCase();

    return this.themes.filter(theme => {
      const matchesState =
        this.stateFilter === 'all' ||
        (this.stateFilter === 'active' && theme.isActive) ||
        (this.stateFilter === 'inactive' && !theme.isActive);
      const matchesSearch =
        !search ||
        theme.name.toLowerCase().includes(search) ||
        theme.themeKey.toLowerCase().includes(search) ||
        theme.description.toLowerCase().includes(search);

      return matchesState && matchesSearch;
    });
  }

  get activeThemeCount(): number {
    return this.themes.filter(theme => theme.isActive).length;
  }

  load(): void {
    this.loading = true;
    this.endpoint.list().subscribe({
      next: themes => {
        this.themes = themes;
        this.loading = false;
        this.errorMessage = '';
      },
      error: () => {
        this.loading = false;
        this.errorMessage = 'Playlist themes could not be loaded.';
      }
    });
  }

  setStateFilter(filter: ThemeStateFilter): void {
    this.stateFilter = filter;
  }

  toggleActive(theme: AdminPlaylistTheme): void {
    if (this.updating.has(theme.id)) {
      return;
    }

    this.updating.add(theme.id);
    this.endpoint.setActive(theme.id, !theme.isActive).subscribe({
      next: updated => {
        this.updating.delete(theme.id);
        this.themes = this.themes.map(item =>
          item.id === updated.id ? updated : item
        );
        this.snackBar.open(
          `${updated.name} is now ${updated.isActive ? 'active' : 'inactive'}.`,
          'Dismiss',
          {duration: 3500}
        );
      },
      error: () => {
        this.updating.delete(theme.id);
      }
    });
  }

  positiveLabelCount(theme: AdminPlaylistTheme): number {
    return theme.labels.filter(label => label.polarity === 'Positive').length;
  }

  negativeLabelCount(theme: AdminPlaylistTheme): number {
    return theme.labels.filter(label => label.polarity === 'Negative').length;
  }
}
