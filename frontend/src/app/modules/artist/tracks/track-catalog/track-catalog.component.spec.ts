import {EMPTY, of} from 'rxjs';
import {TrackCatalogComponent} from './track-catalog.component';

describe('TrackCatalogComponent', () => {
  const response = {
    items: [{
      id: 41,
      title: 'Night drive',
      isExplicit: false,
      lengthSeconds: 185,
      streams: 12,
      featuredArtists: [],
      releaseCount: 0,
      primaryRelease: null
    }],
    page: 1,
    pageSize: 20,
    totalCount: 1,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false
  };

  let endpoint: any;
  let artistHandler: any;
  let router: any;
  let component: TrackCatalogComponent;

  beforeEach(() => {
    endpoint = {listArtistTracks: jasmine.createSpy().and.returnValue(of(response))};
    artistHandler = {getSelectedArtist: jasmine.createSpy().and.returnValue({id: 7, role: 'Viewer'})};
    router = {url: '/artist/tracks', events: EMPTY, navigate: jasmine.createSpy()};
    component = new TrackCatalogComponent(endpoint, artistHandler, router);
  });

  afterEach(() => component.ngOnDestroy());

  it('loads the selected artist lead-track catalog and exposes the response', () => {
    component.ngOnInit();

    expect(endpoint.listArtistTracks).toHaveBeenCalledWith(7, {
      pageNumber: 1,
      pageSize: 20,
      title: undefined,
      primaryReleaseTitle: undefined,
      minStreams: undefined,
      maxStreams: undefined,
      minDurationSeconds: undefined,
      maxDurationSeconds: undefined,
      sortBy: undefined,
      sortDirection: undefined
    });
    expect(component.tracks.map(track => track.id)).toEqual([41]);
    expect(component.response?.totalCount).toBe(1);
  });

  it('gates creation for Viewer while allowing management roles and Admin', () => {
    expect(component.canEdit).toBeFalse();
    artistHandler.getSelectedArtist.and.returnValue({id: 7, role: 'Streaming Manager'});
    expect(component.canEdit).toBeTrue();
    artistHandler.getSelectedArtist.and.returnValue({id: 7, role: 'Admin'});
    expect(component.canEdit).toBeTrue();
  });

  it('sends release, stream and duration filters and converts minutes to seconds', () => {
    component.primaryReleaseControl.setValue('Deluxe', {emitEvent: false});
    component.minStreamsControl.setValue(100, {emitEvent: false});
    component.maxStreamsControl.setValue(5000, {emitEvent: false});
    component.minDurationMinutesControl.setValue(2, {emitEvent: false});
    component.maxDurationMinutesControl.setValue(4.5, {emitEvent: false});

    component.load();

    expect(endpoint.listArtistTracks).toHaveBeenCalledWith(7, {
      pageNumber: 1,
      pageSize: 20,
      title: undefined,
      primaryReleaseTitle: 'Deluxe',
      minStreams: 100,
      maxStreams: 5000,
      minDurationSeconds: 120,
      maxDurationSeconds: 270,
      sortBy: undefined,
      sortDirection: undefined
    });
  });

  it('resets paging and requests server sorting for sortable table columns', () => {
    component.pageNumber = 3;

    component.changeSort({active: 'streams', direction: 'desc'});

    expect(component.pageNumber).toBe(1);
    expect(endpoint.listArtistTracks).toHaveBeenCalledWith(7, jasmine.objectContaining({
      pageNumber: 1,
      sortBy: 'streams',
      sortDirection: 'desc'
    }));
  });

  it('navigates rows to details and the create action to the side-panel route', () => {
    component.openTrack(response.items[0]);
    expect(router.navigate).toHaveBeenCalledWith(['/artist/tracks', 41]);

    component.openCreate();
    expect(router.navigate).toHaveBeenCalledWith(['/artist/tracks/create']);
  });
});
