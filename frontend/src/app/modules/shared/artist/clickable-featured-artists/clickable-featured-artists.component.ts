import {Component, EventEmitter, Input, OnInit, Output} from '@angular/core';
import {ArtistSimpleDto} from '../../../../services/auth-services/dto/artist-dto';
import {ArtistTrackDto} from '../../../../endpoints/track-endpoints/track-get-by-id-endpoint.service';
import {Router} from '@angular/router';

@Component({
  selector: 'app-clickable-featured-artists',
  templateUrl: './clickable-featured-artists.component.html',
  styleUrl: './clickable-featured-artists.component.css'
})
export class ClickableFeaturedArtistsComponent implements OnInit{
  constructor(  private router: Router) {
  }

  ngOnInit(): void {
      //window.addEventListener('contextmenu', (e) => {
      //  e.preventDefault();
      //})
  }
  @Input() artists: ArtistSimpleDto[] | ArtistTrackDto[] | null = null;
  @Input() separator = ", ";
  @Input() replaceUrlOnNavigate = false;
  @Input() scrollWhenOverflow = false;
  @Output() onArtistClick: EventEmitter<ArtistSimpleDto | ArtistTrackDto> = new EventEmitter();

  emitArtist(artist : ArtistSimpleDto | ArtistTrackDto) {
    this.onArtistClick.emit(artist);
    this.router.navigate(
      ["/listener/profile", artist.id],
      {replaceUrl: this.replaceUrlOnNavigate}
    );
  }

  handleRightClick(event: MouseEvent) {

  }
}
