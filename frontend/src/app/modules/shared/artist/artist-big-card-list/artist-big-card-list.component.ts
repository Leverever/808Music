import {Component, Input} from '@angular/core';
import {MyConfig} from '../../../../my-config';
import {ArtistSimpleDto} from '../../../../services/auth-services/dto/artist-dto';
import {Params, Router} from '@angular/router';
import {ArtistInfoResponse} from '../../../../endpoints/user-endpoints/get-user-last-streams-endpoint.service';
import {TrackGetResponse} from '../../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';

@Component({
  selector: 'app-artist-big-card-list',
  templateUrl: './artist-big-card-list.component.html',
  styleUrls: ['../../../listener/artist-page/artist-music-page/artist-music-page.component.css','./artist-big-card-list.component.css']
})
export class ArtistBigCardListComponent {

  protected readonly MyConfig = MyConfig;
  @Input() title: string = "Artists";
  @Input() artists: ArtistSimpleDto[] | null  = null;
  @Input() artist : ArtistInfoResponse [] | null  = null;
  @Input() isProfile = false;
  @Input() numberDescription = "number"
  @Input() queryParams: Params | null = null;
  @Input() cardDescriptions: Record<number, string> = {};
  @Input() cardTooltips: Record<number, string> = {};
  @Input() showViewAll: boolean = true;

  constructor(private router: Router) {
  }

  viewAll(b: boolean) {
    this.router.navigate(["/listener/artists"], {queryParams: this.queryParams});
  }
}
