import {NgModule} from '@angular/core';
import {RouterModule, Routes} from '@angular/router';
import {AdminLayoutComponent} from './admin-layout/admin-layout.component';
import {DestinationComponent} from './destination/destination.component';
import {DashboardComponent} from './dashboard/dashboard.component';
import {ReservationComponent} from './reservation/reservation.component';
import {AdminErrorPageComponent} from './admin-error-page/admin-error-page.component';
import {CitiesComponent} from './cities/cities.component';
import {CitiesEditComponent} from './cities/cities-edit/cities-edit.component';
import {RecurringTasksComponent} from './recurring-tasks/recurring-tasks.component';
import {PlaylistThemesComponent} from './playlist-themes/playlist-themes.component';
import {
  PlaylistThemeEditorComponent
} from './playlist-themes/playlist-theme-editor/playlist-theme-editor.component';

const routes: Routes = [
  {
    path: '',
    component: AdminLayoutComponent,
    children: [
      {path: '', redirectTo: 'overview', pathMatch: 'full'},
      {path: 'overview', component: DashboardComponent},
      {path: 'dashboard', redirectTo: 'overview', pathMatch: 'full'},
      {path: 'recurring-tasks', component: RecurringTasksComponent},
      {path: 'playlist-themes', component: PlaylistThemesComponent},
      {path: 'playlist-themes/new', component: PlaylistThemeEditorComponent},
      {path: 'playlist-themes/:id/edit', component: PlaylistThemeEditorComponent},
      {path: 'cities', component: CitiesComponent},
      {path: 'cities/new', component: CitiesEditComponent},
      {path: 'cities/edit/:id', component: CitiesEditComponent},
      {path: 'destination', component: DestinationComponent},
      {path: 'order', component: ReservationComponent},
      {path: '**', component: AdminErrorPageComponent}
    ]
  }
];

@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class AdminRoutingModule {
}
