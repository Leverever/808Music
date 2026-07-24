import {HttpClientTestingModule, HttpTestingController} from '@angular/common/http/testing';
import {TestBed} from '@angular/core/testing';
import {MyConfig} from '../../my-config';
import {TrackManagementV2EndpointService} from './track-management-v2-endpoint.service';

describe('TrackManagementV2EndpointService', () => {
  let service: TrackManagementV2EndpointService;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({imports: [HttpClientTestingModule]});
    service = TestBed.inject(TrackManagementV2EndpointService);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('uses the v2 artist catalog with server paging and filtering', () => {
    service.listArtistTracks(12, {
      pageNumber: 2,
      pageSize: 20,
      title: 'night',
      primaryReleaseTitle: 'deluxe',
      minStreams: 100,
      maxStreams: 5000,
      minDurationSeconds: 120,
      maxDurationSeconds: 300,
      sortBy: 'primaryRelease',
      sortDirection: 'desc'
    }).subscribe();

    const request = http.expectOne(req => req.url === `${MyConfig.api_address}/api/v2/artists/12/tracks`);
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('pageNumber')).toBe('2');
    expect(request.request.params.get('pageSize')).toBe('20');
    expect(request.request.params.get('title')).toBe('night');
    expect(request.request.params.get('primaryReleaseTitle')).toBe('deluxe');
    expect(request.request.params.get('minStreams')).toBe('100');
    expect(request.request.params.get('maxStreams')).toBe('5000');
    expect(request.request.params.get('minDurationSeconds')).toBe('120');
    expect(request.request.params.get('maxDurationSeconds')).toBe('300');
    expect(request.request.params.get('sortBy')).toBe('primaryRelease');
    expect(request.request.params.get('sortDirection')).toBe('desc');
    request.flush({items: [], page: 2, pageSize: 20, totalCount: 0, totalPages: 0, hasPreviousPage: true, hasNextPage: false});
  });

  it('creates a track with the v2 multipart contract', () => {
    const master = new File(['audio'], 'master.wav', {type: 'audio/wav'});
    service.upload(5, 'New track', true, master).subscribe();

    const request = http.expectOne(`${MyConfig.api_address}/api/v2/tracks/upload`);
    expect(request.request.method).toBe('POST');
    const body = request.request.body as FormData;
    expect(body.get('artistId')).toBe('5');
    expect(body.get('title')).toBe('New track');
    expect(body.get('isExplicit')).toBe('true');
    expect(body.get('masterFile')).toBe(master);
    request.flush({id: 8, title: 'New track', isExplicit: true, mainArtistId: 5, objectKey: 'tracks/8.wav'});
  });

  it('sends complete featured artist and release replacement payloads', () => {
    service.replaceFeaturedArtists(9, [{artistId: 21, showOnProfile: false}]).subscribe();
    const artists = http.expectOne(`${MyConfig.api_address}/api/v2/tracks/9/featured-artists`);
    expect(artists.request.method).toBe('PUT');
    expect(artists.request.body).toEqual({artists: [{artistId: 21, showOnProfile: false}]});
    artists.flush([]);

    const releasesPayload = [{releaseId: 3, discNumber: 1, trackNumber: 2, titleOverride: null, isPrimaryRelease: true}];
    service.replaceReleases(9, releasesPayload).subscribe();
    const releases = http.expectOne(`${MyConfig.api_address}/api/v2/tracks/9/releases`);
    expect(releases.request.method).toBe('PUT');
    expect(releases.request.body).toEqual({releases: releasesPayload});
    releases.flush([]);
  });

  it('uses v2 stem state and analytics endpoints', () => {
    service.activateStemSet(4, 'c991ef41-df41-45c9-8376-b7e15c4f31d7').subscribe();
    const activate = http.expectOne(`${MyConfig.api_address}/api/v2/tracks/4/stems/c991ef41-df41-45c9-8376-b7e15c4f31d7/activate`);
    expect(activate.request.method).toBe('PUT');
    activate.flush({});

    service.getStatistics(4, 90).subscribe();
    const statistics = http.expectOne(req => req.url === `${MyConfig.api_address}/api/v2/tracks/4/statistics`);
    expect(statistics.request.params.get('days')).toBe('90');
    statistics.flush({});
  });
});
