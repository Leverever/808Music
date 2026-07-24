import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import {AlbumListComponent} from './album-list/album-list.component';
import {UnauthorizedComponent} from '../../shared/unauthorized/unauthorized.component';
import {AlbumListMaterialComponent} from './album-list-material/album-list-material.component';
import {AlbumCreateComponent} from './album-create/album-create.component';
import {AlbumCreateOrEditComponent} from './album-create-or-edit/album-create-or-edit.component';
import {AlbumReleaseTracksComponent} from './album-release-tracks/album-release-tracks.component';

const routes: Routes = [
  {
    path: ':id/tracks',
    component: AlbumReleaseTracksComponent
  },
  {
    path: '',
    component: AlbumListMaterialComponent,
    children: [
      {
        path: 'create', component: AlbumCreateOrEditComponent
      },
      {
        path: 'edit/:id', component: AlbumCreateOrEditComponent
      }
    ]
  },
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class AlbumRoutingModule { }
