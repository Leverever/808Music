import {NgModule} from '@angular/core';
import {RouterModule} from '@angular/router';
import {MatButtonModule} from '@angular/material/button';
import {MatIconModule} from '@angular/material/icon';
import {SharedModule} from '../../shared/shared.module';
import {TracksListComponent} from './tracks-list/tracks-list.component';

@NgModule({
  declarations: [TracksListComponent],
  imports: [
    SharedModule,
    RouterModule,
    MatButtonModule,
    MatIconModule
  ],
  exports: [TracksListComponent]
})
export class ReleaseTracksViewModule {}
