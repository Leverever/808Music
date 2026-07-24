import {NgModule} from '@angular/core';
import {CommonModule} from '@angular/common';
import {FormsModule} from '@angular/forms';
import {MatIcon} from '@angular/material/icon';
import {MatProgressSpinner} from '@angular/material/progress-spinner';
import {
  MatAutocomplete,
  MatAutocompleteTrigger,
  MatOption
} from '@angular/material/autocomplete';
import {AdminRoutingModule} from './admin-routing.module';
import {SharedModule} from '../shared/shared.module';
import {DashboardComponent} from './dashboard/dashboard.component';
import {DestinationComponent} from './destination/destination.component';
import {AdminLayoutComponent} from './admin-layout/admin-layout.component';
import {ReservationComponent} from './reservation/reservation.component';
import {AdminErrorPageComponent} from './admin-error-page/admin-error-page.component';
import {CitiesComponent} from './cities/cities.component';
import {CitiesEditComponent} from './cities/cities-edit/cities-edit.component';
import {RecurringTasksComponent} from './recurring-tasks/recurring-tasks.component';
import {PlaylistThemesComponent} from './playlist-themes/playlist-themes.component';
import {
  PlaylistThemeEditorComponent
} from './playlist-themes/playlist-theme-editor/playlist-theme-editor.component';

@NgModule({
  declarations: [
    DashboardComponent,
    DestinationComponent,
    AdminLayoutComponent,
    ReservationComponent,
    AdminErrorPageComponent,
    CitiesComponent,
    CitiesEditComponent,
    RecurringTasksComponent,
    PlaylistThemesComponent,
    PlaylistThemeEditorComponent
  ],
  imports: [
    CommonModule,
    AdminRoutingModule,
    FormsModule,
    SharedModule,
    MatIcon,
    MatProgressSpinner,
    MatAutocomplete,
    MatAutocompleteTrigger,
    MatOption
  ],
  providers: []
})
export class AdminModule {
}
