import {ChangeDetectorRef, Component, HostListener, OnInit} from '@angular/core';
import {Router} from '@angular/router';
import {ArtistHandlerService} from '../../../services/artist-handler.service';
import {MyConfig} from '../../../my-config';
import {UserProfileService} from '../../../endpoints/auth-endpoints/user-profile-endpoint.service';
import {MyUserAuthService} from '../../../services/auth-services/my-user-auth.service';

@Component({
  selector: 'artist-sidenav',
  templateUrl: './artist-sidenav.component.html',
  styleUrls: ['./artist-sidenav.component.css']
})
export class ArtistSidenavComponent implements OnInit {
  isMenuVisible: boolean = false;
  pathToPfp: string = `${MyConfig.api_address}`;
  pathToUserPfp: string = "";

  constructor(private router: Router, private artistHandlerService: ArtistHandlerService,
              private auth: MyUserAuthService,
              private userProfileService: UserProfileService,
              private cdRef: ChangeDetectorRef,) {}

  ngOnInit(): void {
    let userId = this.auth.getAuthToken()?.userId;
    if (userId) {
      this.userProfileService.getProfilePicture(userId).subscribe({
        next: (response) => {
          if (response && response.profilePicturePath) {
            this.pathToUserPfp = `${MyConfig.media_address}${response.profilePicturePath}`;
            console.log(this.pathToUserPfp);
            this.pathToPfp = this.artistHandlerService.isArtistSelected() ? this.pathToPfp : this.pathToUserPfp;
          }
        },
        error: (error) => {
          console.error('Error fetching profile picture:', error);
        }
    });
    }

    let artist = this.artistHandlerService.getSelectedArtist();
    if(artist) {
     this.pathToPfp = `${MyConfig.api_address}${artist.pfpPath}`;
    }


    this.artistHandlerService.artistSelectedPfp$.subscribe(data => {
      if(data != "")
      {
       this.pathToPfp = data;
      }
      else {
        this.pathToPfp = this.pathToUserPfp;
      }
    });
  }

  toggleMenu(): void {
    console.log('Toggling menu visibility...');
    this.isMenuVisible = !this.isMenuVisible;
  }

  navigateTo(destination: string): void {
    if (destination === 'products') {
      const selectedArtistName = this.artistHandlerService.getSelectedArtist();
      console.log('Selected Artist Name:', selectedArtistName);

      if (selectedArtistName) {
        this.router.navigate([`/artist/${selectedArtistName.name}/products/${selectedArtistName.id}`]);
      } else {
        console.error('No artist selected');
        alert('No artist selected.');
      }
    } else {
      this.router.navigate([destination]);
    }
    this.isMenuVisible = false;
  }

  getArtistProductPath() {
    if(!this.router.url.includes('/products')) {
      return "";
    }

    let artistName = this.artistHandlerService.getSelectedArtist()?.name;
    if(artistName)
    {
      artistName = artistName.replaceAll(" ", "%20");

      console.log(`/artist/${artistName}/products`);
      return `/artist/${artistName}/products`;
    }
    return "";
  }


  @HostListener('document:click', ['$event'])
  onClickOutside(event: Event): void {
    const clickedElement = event.target as HTMLElement;
    const isInsideMenu =
      clickedElement.closest('.profile-menu') || clickedElement.classList.contains('profile-picture');

    if (!isInsideMenu) {
      this.isMenuVisible = false;
    }
  }

  switchArtist() {

    sessionStorage.removeItem('artist');
    this.router.navigate([`/artist/dashboard`]);
  }

    protected readonly MyConfig = MyConfig;
}

