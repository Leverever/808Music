import {Component, EventEmitter, Input, OnInit, Output} from '@angular/core';
import {MyConfig} from '../../../my-config';
import {MyPagedList} from '../../../services/auth-services/dto/my-paged-list';
import {AlbumGetAllResponse} from '../../../endpoints/album-endpoints/album-get-all-endpoint.service';
import {ActivatedRoute, Params, Router} from '@angular/router';
import {TrackGetAllEndpointService} from '../../../endpoints/track-endpoints/track-get-all-endpoint.service';
import {MusicPlayerService} from '../../../services/music-player.service';
import {ConfirmDialogComponent} from '../dialogs/confirm-dialog/confirm-dialog.component';
import {MatDialog} from '@angular/material/dialog';
import {MatSnackBar} from '@angular/material/snack-bar';
import {AlbumDeleteEndpointService} from '../../../endpoints/album-endpoints/album-delete-endpoint.service';
import {ArtistHandlerService} from '../../../services/artist-handler.service';

@Component({
  selector: 'app-album-card-list',
  templateUrl: './album-card-list.component.html',
  styleUrls: ['../../listener/artist-page/artist-music-page/artist-music-page.component.css','./album-card-list.component.css']
})
export class AlbumCardListComponent implements OnInit {
  protected readonly MyConfig = MyConfig;
  @Input() albums: MyPagedList<AlbumGetAllResponse> | null = null;
  @Input() title: string = "RELEASES";
  @Input() queryParams: Params | null = null;
  @Input() cardSubtitles: Record<number, string> = {};
  @Input() showViewAll: boolean = true;
  artistMode: boolean = false;
  role = "";
  @Output() deletedAlbum = new EventEmitter<boolean>();

  constructor(private router: Router,
              private trackGetAllService: TrackGetAllEndpointService,
              private musicPlayerService: MusicPlayerService,
              private route: ActivatedRoute,
              private dialog: MatDialog,
              private snackBar: MatSnackBar,
              private albumDeleteService : AlbumDeleteEndpointService,
              private artistHandler : ArtistHandlerService) {
  }

  ngOnInit(): void {
    if(this.router.url.includes("/artist/search"))
    {
      this.artistMode = true;
      this.role = this.artistHandler.getSelectedArtist()?.role ?? "";
    }
  }

  goToAlbum(id: number) {
    if(!this.artistMode)
    {
      this.router.navigate(["listener/release", id])
    }
    else
    {
      this.router.navigate(["artist/tracks/", id])
    }
  }

  getTitle(title: string | undefined) {
    if(title == undefined)
    {
      return "";
    }
    let i = title.indexOf(" ");
    if(i <= -1 && title.length > 20 || i > 20 )
    {
      return title.substring(0, 17) + "...";
    }
    return title;
  }

  playAlbum(id: number, featured: boolean = false) {
    let a = this.albums?.dataItems.find(item => item.id == id)
    if(a)
    {
      this.trackGetAllService.handleAsync({albumId: id}).subscribe({
        next: value => {
          this.musicPlayerService.createQueue(value.dataItems,{display:a?.title + " - " + a?.type, value:"/listener/release/"+id});
        }
      })
    }
  }

  getYear(releaseDate: string) {
    return new Date(releaseDate).getFullYear();
  }

  getSubtitle(album: AlbumGetAllResponse): string {
    if(Object.prototype.hasOwnProperty.call(this.cardSubtitles, album.id))
    {
      return this.cardSubtitles[album.id];
    }

    return `${album.type} • ${this.getYear(album.releaseDate)}`;
  }

  getImageUrl(path: string): string {
    return /^https?:\/\//i.test(path) ? path : MyConfig.api_address + path;
  }

  viewAll(featured: boolean) {
    this.router.navigate(["listener/releases"],{queryParams: this.queryParams});
  }

  goToEdit(id: number) {
    this.router.navigate(["artist/album/edit", id]);
  }

  deleteAlbum(id: number) {

    let matRef = this.dialog.open(ConfirmDialogComponent, {hasBackdrop: true,
      data:{
        title: "Are you sure you want to delete this album",
        content: "This will delete every track that is in the album.",
      }})

    matRef.afterClosed().subscribe(res => {
      if(res)
      {
        this.albumDeleteService.handleAsync(id).subscribe({
          error: () => { alert("Album deletion failed."); },
          complete: () => {
            this.snackBar.open("Album deleted successfully.", "Dismiss", {
              duration: 3000
            });
            this.ngOnInit();
            this.deletedAlbum.emit(true);
          }
        });
      }
    })
  }

  getReleaseDateTime(releaseDate: string) {
    return new Date(releaseDate).getTime();
  }
}
