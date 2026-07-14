import { NgModule } from '@angular/core';
import { CommonModule } from '@angular/common';
import { PlaylistRoutingModule } from './playlist-routing.module';
import { PlaylistListMaterialComponent } from './playlist-list/playlist-list-material.component';
import {PlaylistCreateOrEditComponent} from './playlist-create/playlist-create-or-edit.component';
import {MatFormField, MatFormFieldModule} from '@angular/material/form-field';
import {MatSlideToggle} from '@angular/material/slide-toggle';
import {SharedModule} from '../../shared/shared.module';
import {MatIcon} from '@angular/material/icon';
import {MatInput} from '@angular/material/input';
import {MatAnchor, MatButton, MatIconAnchor, MatIconButton} from '@angular/material/button';
import {MatCard, MatCardContent, MatCardHeader, MatCardTitle, MatCardTitleGroup} from '@angular/material/card';
import {TracksPageComponent} from './tracks-page/tracks-page.component';
import {MatAutocomplete, MatAutocompleteTrigger, MatOption} from '@angular/material/autocomplete';
import {MatMenu, MatMenuItem, MatMenuTrigger} from '@angular/material/menu';
import { PlaylistUpdateDialogComponent } from './tracks-page/playlist-update-dialog/playlist-update-dialog.component';
import {MatDialogActions, MatDialogContent, MatDialogTitle} from '@angular/material/dialog';
import { PlaylistCreateDialogComponent } from './tracks-page/playlist-create-dialog/playlist-create-dialog.component';
import {MatTooltip} from '@angular/material/tooltip';
import { AddCollaboratorDialogComponent } from './tracks-page/add-collaborator-dialog/add-collaborator-dialog.component';
import { AddCollaboratorComponent } from './add-collaborator/add-collaborator.component';
import {ListenerModule} from '../../listener/listener.module';
import { CollaboratorListDialogComponent } from './tracks-page/collaborator-list-dialog/collaborator-list-dialog.component';
import {MatDivider} from "@angular/material/divider";


@NgModule({
    declarations: [PlaylistListMaterialComponent, PlaylistCreateOrEditComponent, TracksPageComponent, PlaylistUpdateDialogComponent, PlaylistCreateDialogComponent, AddCollaboratorDialogComponent, AddCollaboratorComponent, CollaboratorListDialogComponent],
    exports: [
        TracksPageComponent
    ],
    imports: [
        CommonModule,
        PlaylistRoutingModule,
        MatFormField,
        MatSlideToggle,
        SharedModule,
        MatIcon,
        MatInput,
        MatButton,
        MatAnchor,
        MatCardHeader,
        MatCard,
        MatCardTitle,
        MatCardContent,
        MatCardTitleGroup,
        MatFormFieldModule,
        MatIconAnchor,
        MatIconButton,
        MatAutocomplete,
        MatOption,
        MatAutocompleteTrigger,
        MatMenu,
        MatMenuTrigger,
        MatMenuItem,
        MatDialogActions,
        MatDialogContent,
        MatDialogTitle,
        MatTooltip,
        ListenerModule,
        MatDivider
    ]
})
export class PlaylistModule { }
