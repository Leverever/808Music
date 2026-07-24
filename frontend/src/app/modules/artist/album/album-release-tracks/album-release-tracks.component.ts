import {Component} from '@angular/core';

@Component({
  selector: 'app-album-release-tracks',
  template: '<app-tracks-page [artistMode]="true" [isHome]="true"></app-tracks-page>'
})
export class AlbumReleaseTracksComponent {}
