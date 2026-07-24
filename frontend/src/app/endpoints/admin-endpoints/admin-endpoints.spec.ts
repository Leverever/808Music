import {HttpClientTestingModule, HttpTestingController} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';
import {MyConfig} from '../../my-config';
import {AdminRecurringTasksEndpointService} from './admin-recurring-tasks-endpoint.service';
import {
  AdminPlaylistThemesEndpointService,
  CreateAdminPlaylistThemeRequest
} from './admin-playlist-themes-endpoint.service';

describe('Admin endpoint services', () => {
  let http: HttpTestingController;
  let recurringTasks: AdminRecurringTasksEndpointService;
  let playlistThemes: AdminPlaylistThemesEndpointService;

  beforeEach(() => {
    TestBed.configureTestingModule({imports: [HttpClientTestingModule]});
    http = TestBed.inject(HttpTestingController);
    recurringTasks = TestBed.inject(AdminRecurringTasksEndpointService);
    playlistThemes = TestBed.inject(AdminPlaylistThemesEndpointService);
  });

  afterEach(() => http.verify());

  it('lists and runs registered recurring tasks', () => {
    recurringTasks.list().subscribe();
    const list = http.expectOne(`${MyConfig.api_address}/api/v2/admin/recurring-tasks`);
    expect(list.request.method).toBe('GET');
    list.flush([]);

    recurringTasks.run('daily automatic/playlists').subscribe();
    const run = http.expectOne(
      `${MyConfig.api_address}/api/v2/admin/recurring-tasks/daily%20automatic%2Fplaylists/run`
    );
    expect(run.request.method).toBe('POST');
    expect(run.request.body).toBeNull();
    run.flush({
      name: 'daily automatic/playlists',
      startedAt: '2026-07-24T08:00:00Z',
      completedAt: '2026-07-24T08:01:00Z',
      status: 'Completed'
    });
  });

  it('uses the admin theme management contract', () => {
    const request: CreateAdminPlaylistThemeRequest = {
      themeKey: 'focus-flow',
      name: 'Focus Flow',
      description: 'Steady tracks for concentration.',
      isActive: true,
      trackCount: 25,
      sortOrder: 10,
      labels: [{
        label: 'focus',
        polarity: 'Positive',
        source: 'EssentiaTag',
        tagNamespace: 'moodtheme',
        weight: 1
      }]
    };

    playlistThemes.create(request).subscribe();
    const create = http.expectOne(`${MyConfig.api_address}/api/v2/admin/playlist-themes`);
    expect(create.request.method).toBe('POST');
    expect(create.request.body).toEqual(request);
    create.flush({...request, id: 'theme-id', createdAt: '', updatedAt: ''});

    playlistThemes.setActive('theme/id', false).subscribe();
    const active = http.expectOne(
      `${MyConfig.api_address}/api/v2/admin/playlist-themes/theme%2Fid/active`
    );
    expect(active.request.method).toBe('PATCH');
    expect(active.request.body).toEqual({isActive: false});
    active.flush({...request, id: 'theme/id', isActive: false, createdAt: '', updatedAt: ''});

    playlistThemes.getTagCatalog().subscribe();
    const catalog = http.expectOne(
      `${MyConfig.api_address}/api/v2/admin/playlist-themes/tag-catalog`
    );
    expect(catalog.request.method).toBe('GET');
    catalog.flush([{namespace: 'moodtheme', labels: ['focus']}]);
  });
});
