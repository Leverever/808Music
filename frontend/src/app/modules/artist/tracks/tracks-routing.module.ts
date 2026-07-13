import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import {TracksLayoutComponent} from './tracks-layout/tracks-layout.component';
import {TracksCreateOrEditComponent} from './tracks-create-or-edit/tracks-create-or-edit.component';
import {TrackCatalogComponent} from './track-catalog/track-catalog.component';
import {TrackDetailsComponent} from './track-details/track-details.component';
import {TrackStatisticsComponent} from './track-statistics/track-statistics.component';

const routes: Routes = [
  {path: '', component: TracksLayoutComponent, children: [
    {path: 'create', component: TrackCatalogComponent, children: [
        {path: '', component: TracksCreateOrEditComponent}
      ]},
    {path: ':trackId/statistics', component: TrackStatisticsComponent},
    {path: ':trackId', component: TrackDetailsComponent},
    {path: '', component: TrackCatalogComponent}
    ]}
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class TracksRoutingModule { }
