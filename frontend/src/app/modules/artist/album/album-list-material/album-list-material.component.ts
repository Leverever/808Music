import {Component, HostListener, inject, Input, OnInit, ViewChild} from '@angular/core';
import {
  AlbumGetAllEndpointService,
  AlbumGetAllResponse, AlbumPagedRequest
} from '../../../../endpoints/album-endpoints/album-get-all-endpoint.service';
import {AlbumDeleteEndpointService} from '../../../../endpoints/album-endpoints/album-delete-endpoint.service';
import {ActivatedRoute, NavigationStart, Router} from '@angular/router';
import {MyUserAuthService} from '../../../../services/auth-services/my-user-auth.service';
import {ArtistHandlerService} from '../../../../services/artist-handler.service';
import {MyPagedList} from '../../../../services/auth-services/dto/my-paged-list';
import {MyConfig} from "../../../../my-config";
import {MatDialog} from "@angular/material/dialog";
import {MatSnackBar} from "@angular/material/snack-bar";
import {ConfirmDialogComponent} from "../../../shared/dialogs/confirm-dialog/confirm-dialog.component";
import {PageEvent} from "@angular/material/paginator";
import {
  AlbumType,
  AlbumTypeGetAllEndpointService
} from "../../../../endpoints/album-endpoints/album-type-get-all-endpoint.service";
import {MatChipOption, MatChipSelectionChange} from "@angular/material/chips";
import {HttpErrorResponse} from "@angular/common/http";
import {MatCheckboxChange} from "@angular/material/checkbox";
import {FormControl} from '@angular/forms';
import {TrackGetAllEndpointService} from '../../../../endpoints/track-endpoints/track-get-all-endpoint.service';
import {MusicPlayerService} from '../../../../services/music-player.service';
import {Location} from '@angular/common';
import {MAT_DATE_LOCALE, provideNativeDateAdapter} from '@angular/material/core';
import {AlbumSpotlightEndpointService} from '../../../../endpoints/album-endpoints/album-spotlight-endpoint.service';

@Component({
  selector: 'app-album-list-material',
  templateUrl: './album-list-material.component.html',
  styleUrls: ['../../choose-profile/choose-profile.component.css','../../artist-layout/artist-layout.component.css','./album-list-material.component.css'],
  providers: [provideNativeDateAdapter(), {provide: MAT_DATE_LOCALE, useValue: 'utc'}]
})
export class AlbumListMaterialComponent implements OnInit {
  @Input() title = "Your releases";
  @Input() hasControls: boolean = true;
  selectedAlbum: boolean = true;
  pagedList : MyPagedList<AlbumGetAllResponse> | null = null;
  albums: AlbumGetAllResponse[] | null = null;
  albumTypes : AlbumType[] | null = null;
  filteredAlbumTypes : AlbumType[] = [];
  dialog = inject(MatDialog);
  snackBar = inject(MatSnackBar);
  pagedRequest : AlbumPagedRequest = {
    title: "",
  }
  defaultPageSize = 18;
  albumTitleQuery = new FormControl("");
  periodTo = new FormControl<Date | null>(null);
  periodFrom = new FormControl<Date | null>(null);

  isResult: boolean = false;

  @Input() isHome : boolean = false;

  artistRole : string = "";

  @ViewChild('All') chip! : MatChipOption;

  constructor(private albumService: AlbumGetAllEndpointService,
              private albumDeleteService: AlbumDeleteEndpointService,
              private router: Router,
              private auth: MyUserAuthService,
              private artistHandler: ArtistHandlerService,
              private albumTypeGet : AlbumTypeGetAllEndpointService,
              private route : ActivatedRoute,
              private getTracksService: TrackGetAllEndpointService,
              private musicPlayerService: MusicPlayerService,
              private location: Location,
              private spotlightService : AlbumSpotlightEndpointService)
  {
  }

  ngOnInit(): void {
    if(this.hasControls)
    {
      this.checkIfHome();
      this.homeCheck(this.router.url);
      this.artistRole = this.artistHandler.getSelectedArtist()?.role ?? "";
    }

    this.periodFrom.valueChanges.subscribe(value => {
      this.pagedRequest.periodFrom = this.getDateISOString(value) ?? undefined;
      if(this.periodTo.value != null && value != null)
      {
        if(this.periodTo.value < value)
        {
          return;
        }
      }
      this.reloadData();
    })
    this.periodTo.valueChanges.subscribe(value => {
      this.pagedRequest.periodTo = this.getDateISOString(value) ?? undefined;
      if(this.periodFrom.value != null && value != null)
      {
        if(this.periodFrom.value > value)
        {
          return;
        }
      }
      this.reloadData();
    })

    if(this.hasControls)
    {
      this.pagedRequest.artistId = this.artistHandler.getSelectedArtist()?.id;
      this.pagedRequest.getTrackCount = true;
    }
    else {
      this.route.params.subscribe(params => {
          let id = params["id"];
          if(id)
          {
            this.route.queryParams.subscribe(queryParams => {
              let featured = queryParams['featured'];
              if(featured){
                this.pagedRequest.featuredArtistId = id;
              }
              else {
                this.pagedRequest.artistId = id;
              }
            })
            this.pagedRequest.isReleased = true;
          }
          else {
            this.route.queryParams.subscribe(queryParams => {
              let popular = queryParams['popular'];
              if(popular == "yes")
              {
                this.pagedRequest.sortByPopularity = true;
              }
            })

            this.route.queryParams.subscribe(queryParams => {
              let title = queryParams['albumTitle'];
              if(title != null && title.length > 0)
              {
                this.pagedRequest.title = title;
                this.isResult = true;
              }
            })

            this.pagedRequest.isReleased = true;
          }
      })
    }
    this.pagedRequest.pageNumber = 1;
    this.pagedRequest.pageSize = this.defaultPageSize;

    this.reloadData();

    this.albumTypeGet.handleAsync(null).subscribe(t => {
      this.albumTypes = t;
    });

  }

  getDateISOString(value: Date | null) {
      if(value == null)
      {
        return null;
      }

      let date = value as Date;
      let z = date.getTimezoneOffset() * 60 * 1000;
      let toLocal = date.getTime()-z;
      let newDate = new Date(toLocal);
      return newDate.toISOString();
  }

  deleteAlbum(id: number) {
    let album = this.albums?.find(x => x.id === id);

    let matRef = this.dialog.open(ConfirmDialogComponent, {hasBackdrop: true,
    data:{
      title: "Are you sure you want to delete " + album?.title,
      content: "This will delete every track that is in the album.",
    }})

    matRef.afterClosed().subscribe(res => {
      if(res)
      {
        this.albumDeleteService.handleAsync(id).subscribe({
          error: () => { alert("Album deletion failed."); },
          complete: () => {
            this.snackBar.open(album?.title + " deleted successfully.", "Dismiss", {
              duration: 3000
            });
            this.ngOnInit();
          }
        });
      }
    })
  }

  editAlbum(id: number) {
    this.router.navigate(['artist/album/edit', id]);
  }

  getDateString(date: string) :string {
    return new Date(date).toLocaleDateString('en-GB', {weekday:"long", month:"numeric", day:"numeric", year:"numeric"});
  }

  protected readonly MyConfig = MyConfig;

  getReleaseYear(releaseDate: string) {
    return new Date(releaseDate).getFullYear().toString();
  }

  showStats(id: number) {
    this.spotlightService.handleAsync({albumId: id}).subscribe({
      next: (data) =>{
        this.reloadData();
        this.snackBar.open("Successfully highlighted release!", "Dismiss", {duration: 2000});
      }
    })
  }

  switchPage(e: PageEvent) {
    this.pagedRequest.pageNumber = e.pageIndex+1;
    this.pagedRequest.pageSize = e.pageSize;

    this.albumService.handleAsync(this.pagedRequest).subscribe({
      next: data => {
        this.pagedList = data;
        this.albums = data.dataItems;
      }
    })
  }

  openCreate(s: string) {
    this.router.navigate([s]);
  }

  filterSelectedType(e: MatChipSelectionChange) {
    if(e.source.id === "0")
    {
      e.source.select();
      this.pagedRequest.typeId = undefined;
    }
    else if(e.selected)
    {
      this.pagedRequest.typeId = (e.source.value as AlbumType).id;
    }
    else {
      this.pagedRequest.typeId = undefined;
    }
    this.reloadData();
  }

  filterReleaseStatus(e: MatChipSelectionChange) {
    if(e.source.id === "0")
    {
      e.source.select();
      this.pagedRequest.isReleased = undefined;
      this.pagedRequest.getUnlisted = undefined;
    }
    else if(!e.selected && e.isUserInput)
    {
      this.chip.select();
      this.pagedRequest.isReleased = undefined;
      this.pagedRequest.getUnlisted = undefined;
      return;
    }
    /*
    else if(e.source.id)
    {
      this.pagedRequest.isReleased = e.source.value as boolean;
    }
    else {
      this.pagedRequest.isReleased = undefined;
    }

     */

    if(e.source.id === "3")
    {
      this.pagedRequest.isReleased = true;
      this.pagedRequest.artistMode = true;
      this.pagedRequest.getUnlisted = true;
    }

    if(e.source.id === "1")
    {
      this.pagedRequest.isReleased = e.source.value as boolean;
      this.pagedRequest.artistMode = undefined;
      this.pagedRequest.getUnlisted = undefined;
    }

    if(e.source.id === "2")
    {
      this.pagedRequest.isReleased = e.source.value as boolean;
      this.pagedRequest.artistMode = undefined;
      this.pagedRequest.getUnlisted = undefined;
    }

    this.reloadData();
  }

  reloadData() {
    this.albumService.handleAsync(this.pagedRequest).subscribe({
      next: data => {
        this.pagedList = data;
        this.albums = data.dataItems;
        console.log(data);
      },
      error: (err:HttpErrorResponse) => {
        this.snackBar.open(err.error, "Dismiss", {
          duration: 2500
        })
      }
    })
  }

  filterSearch(queryString: string) {
    this.pagedRequest.title = queryString ?? "";
    this.reloadData();
  }

  checkIfHome() {
    this.router.events.subscribe(event => {
      if(event instanceof NavigationStart)
      {
        let home = this.homeCheck(event.url);
        if(home)
        {
          this.reloadData();
        }
      }
    })
  }

  homeCheck(url: string) {
    this.isHome = url == "/artist/album";
    return url == "/artist/album";
  }

  checkoutTracks(e: number) {
    if(this.hasControls)
    {
      this.router.navigate(["/artist/album", e, "tracks"])
    }
    else {
      this.router.navigate(["/listener/release/", e])
    }
  }

  playAlbum(id: number) {
    if(!this.hasControls)
    {
      let a = this.albums?.find(val => val.id === id)
      this.getTracksService.handleAsync({albumId:id, pageSize:1000}).subscribe({
        next: data => {
          this.musicPlayerService.createQueue(data.dataItems,
            {display: a?.title ?? "" + a?.type ?? "Album", value:"/listener/release/"+a?.id})
        }
      })
    }
  }

  goBack() {
    this.location.back();
  }

  removeDate(periodTo: FormControl<Date | null>) {
    periodTo.setValue(null);
  }

  getMillis(releaseDate: string) {
    return (new Date(releaseDate)).getTime();
  }
}
