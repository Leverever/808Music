import {CUSTOM_ELEMENTS_SCHEMA, NgModule} from '@angular/core';
import { CommonModule } from '@angular/common';

import { AlbumRoutingModule } from './album-routing.module';
import { AlbumListMaterialComponent } from './album-list-material/album-list-material.component';
import { MatTableModule } from '@angular/material/table';
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatSortModule } from '@angular/material/sort';
import { AlbumCreateOrEditComponent } from './album-create-or-edit/album-create-or-edit.component';
import {MatAnchor, MatButton, MatFabButton, MatIconAnchor, MatIconButton} from '@angular/material/button';
import {
  MatCard, MatCardActions,
  MatCardContent,
  MatCardHeader,
  MatCardImage, MatCardSmImage,
  MatCardTitle,
  MatCardTitleGroup
} from '@angular/material/card';
import {MatIcon} from '@angular/material/icon';
import {SharedModule} from '../../shared/shared.module';
import {MatChipListbox, MatChipOption} from '@angular/material/chips';
import {MatDivider} from '@angular/material/divider';
import {MatCheckbox} from '@angular/material/checkbox';
import {MatFormField, MatPrefix, MatSuffix} from '@angular/material/form-field';
import {MatInput} from '@angular/material/input';
import {
  MatCalendar,
  MatDatepicker,
  MatDatepickerContent,
  MatDatepickerInput,
  MatDatepickerToggle, MatDateRangeInput, MatDateRangePicker
} from '@angular/material/datepicker';
import {MatOption, MatSelect} from '@angular/material/select';
import {ReleaseTracksViewModule} from '../tracks/release-tracks-view.module';
import {AlbumReleaseTracksComponent} from './album-release-tracks/album-release-tracks.component';


@NgModule({
    declarations: [
        AlbumListMaterialComponent,
        AlbumCreateOrEditComponent,
        AlbumReleaseTracksComponent
    ],
  imports: [
    CommonModule,
    AlbumRoutingModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatAnchor,
    MatCard,
    MatCardContent,
    MatCardHeader,
    MatCardTitle,
    MatCardTitleGroup,
    MatIcon,
    MatCardImage,
    MatCardActions,
    MatButton,
    MatCardSmImage,
    SharedModule,
    MatChipListbox,
    MatChipOption,
    MatDivider,
    MatCheckbox,
    MatFabButton,
    MatFormField,
    MatInput,
    MatDatepickerToggle,
    MatDatepicker,
    MatDatepickerInput,
    MatSelect,
    MatOption,
    MatCalendar,
    MatDatepickerContent,
    MatSuffix,
    MatIconButton,
    MatIconAnchor,
    MatDateRangeInput,
    MatDateRangePicker,
    MatPrefix,
    ReleaseTracksViewModule
  ],
    exports: [
        AlbumListMaterialComponent
    ],
    schemas: [CUSTOM_ELEMENTS_SCHEMA]
})
export class AlbumModule { }
