import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { Location } from '@angular/common';
import { UserProfileService } from '../../../endpoints/auth-endpoints/user-profile-endpoint.service';
import { MyConfig } from '../../../my-config';
import { ActivatedRoute, Router } from '@angular/router';
import {
  GetPlaylistsByUserIdEndpointService,
  PlaylistResponse
} from '../../../endpoints/playlist-endpoints/get-playlist-by-user-endpoint.service';
import {
  ToggleFollowRequest,
  ToggleFollowResponse,
  ToggleFollowService
} from '../../../endpoints/user-endpoints/follow-other-users-endpoint.service';
import {
  IsFollowingRequest,
  IsFollowingService
} from '../../../endpoints/user-endpoints/is-following-user-endpoint.service';
import { UserFollowService } from '../../../endpoints/user-endpoints/get-user-followage-endpoint.service';
import { UserHeaderColorService } from '../../../endpoints/user-endpoints/user-header-color-endpoint.service';
import {animate, style, transition, trigger} from '@angular/animations';
import {
  ArtistInfoResponse,
  GetUserLastStreamsEndpointService
} from '../../../endpoints/user-endpoints/get-user-last-streams-endpoint.service';

@Component({
  selector: 'app-user-profile-page',
  templateUrl: './user-profile-page.component.html',
  styleUrl: './user-profile-page.component.css',
  animations: [
    trigger('pageAnimation', [
      transition(':enter', [
        style({ opacity: 0 }),
        animate('0.4s ease-out', style({ opacity: 1 }))
      ]),
      transition(':leave', [
        style({ opacity: 1 }),
        animate('0.5s ease-in', style({ opacity: 0 }))
      ])
    ]),
    trigger('profileImageAnimation', [
      transition(':enter', [
        style({ transform: 'scale(0)', opacity: 0 }),
        animate('0.3s ease-out', style({ transform: 'scale(1)', opacity: 1 }))
      ])
    ])
  ]

})
export class UserProfilePageComponent implements OnInit {
  pathToPfp = MyConfig.media_address;
  username = '';
  playlists: PlaylistResponse[] | null = null;
  isFollowing = false;
  followers: any[] = [];
  followingUsers: any[] = [];
  isUsersPage = false;
  maxVisibleFollowage = 6;
  selectedColor: string = '#ffffff';
  showAllFollowers = false;
  showAllFollowing = false;
  userId = 0;
  lastStreamed : ArtistInfoResponse[] = [];
  constructor(
    private router: ActivatedRoute,
    private playlistService: GetPlaylistsByUserIdEndpointService,
    private userProfileService: UserProfileService,
    private toggleFollowService: ToggleFollowService,
    private isFollowingService: IsFollowingService,
    private cdr: ChangeDetectorRef,
    private userFollowService: UserFollowService,
    private route: Router,
    private location: Location,
    private userHeaderColorService: UserHeaderColorService,
    private lastStreams : GetUserLastStreamsEndpointService
  ) {}

  ngOnInit(): void {

    this.router.params.subscribe(params => {
      this.userId = params['id'];
      this.updateProfile(this.userId);
    });
  }

  private updateProfile(userId: number): void {
    this.isUsersPage = this.getUserIdFromToken() === Number(userId);
    this.userHeaderColorService.getHeaderColor(userId).subscribe({
      next: (response) => {
        this.selectedColor = response.headerColor;
      },
      error: (err) => {
        console.error('Error fetching header color:', err);
      },
    });
    this.userProfileService.getProfilePicture(userId).subscribe(
      (response) => {
        if (response) {
          this.username = response.username;
          if (response.profilePicturePath) {
            this.pathToPfp = MyConfig.media_address + response.profilePicturePath;
          }
        }
      },
      (error) => {
        console.error('Error fetching profile picture:', error);
      }
    );

    const request: IsFollowingRequest = {
      followerUserId: this.getUserIdFromToken(),
      followedUserId: userId,
    };
    this.isFollowingService.checkIfFollowing(request).subscribe({
      next: (response) => {
        this.isFollowing = response.isFollowing;
        this.cdr.detectChanges();
      },
      error: (err) => {
        console.error('Error:', err);
      },
    });

    this.userFollowService.getFollowingAndFollowers(userId).subscribe(
      (response) => {
        this.followingUsers = response.following;
        this.followers = response.followers;
      },
      (error) => {
        console.error('Error fetching following and followers:', error);
      }
    );
    this.lastStreams.getUserLastStreams(userId).subscribe({
      next: (response) => {
        this.lastStreamed = response;
        console.log(this.lastStreamed);
      }
    })
    this.playlistService.handleAsync(userId).subscribe(playlists => {
      this.playlists = (playlists || []).filter(playlist => playlist.isPublic);
    });
  }

  private getUserIdFromToken(): number {
    let authToken = sessionStorage.getItem('authToken');

    if (!authToken) {
      authToken = localStorage.getItem('authToken');
    }

    if (!authToken) {
      return 0;
    }

    try {
      const parsedToken = JSON.parse(authToken);
      return parsedToken.userId;
    } catch (error) {
      console.error('Error parsing authToken:', error);
      return 0;
    }
  }

  isFollowingUser() {
    this.router.params.subscribe(params => {
      const request: IsFollowingRequest = {
        followerUserId: this.getUserIdFromToken(),
        followedUserId: params['id'],
      };
      this.isFollowingService.checkIfFollowing(request).subscribe({
        next: (response) => {
          this.isFollowing = response.isFollowing;
          this.cdr.detectChanges();
        },
        error: (err) => {
          console.error('Error:', err);
        },
      });
    });
  }

  follow() {
    this.router.params.subscribe(params => {
      const request: ToggleFollowRequest = {
        userId: this.getUserIdFromToken(),
        followedUserId: params['id'],
      };

      this.toggleFollowService.toggleFollow(request).subscribe({
        next: (response: ToggleFollowResponse) => {
          console.log(response.message);
          if (response.isFollowing) {
            console.log('You are now following the user.');
            this.isFollowingUser();
            this.loadFollowersAndFollowing();
          } else {
            console.log('You have unfollowed the user.');
            this.isFollowingUser();
            this.loadFollowersAndFollowing();
          }
        },
        error: (err) => {
          console.error('An error occurred:', err);
        },
      });
    });
  }

  private loadFollowersAndFollowing() {
    this.router.params.subscribe(params => {
      const userId = params['id'];
      this.userFollowService.getFollowingAndFollowers(userId).subscribe(
        (response) => {
          console.log('API Response:', response);
          this.followingUsers = response.following;
          this.followers = response.followers;
          console.log('Following:', this.followingUsers);
          console.log('Followers:', this.followers);
        },
        (error) => {
          console.error('Error fetching following and followers:', error);
        }
      );
    });
  }

  openPlaylist(id: number) {
    this.route.navigate([`/listener/playlist/${id}`]);
  }

  openProfile(userId: any) {
    this.route.navigate(['listener/user/', userId]);
  }

  isUserPage() {
    return this.isUsersPage;
  }

  openArtistProfile(artistId: any) {
    this.route.navigate(['listener/profile/', artistId]);
  }

  onColorChange() {
    this.userHeaderColorService.updateHeaderColor(this.getUserIdFromToken(), this.selectedColor).subscribe({
      next: () => {
        console.log('Header color updated successfully.');
      },
      error: (err) => {
        console.error('Failed to update header color:', err);
      },
    });
  }

  protected readonly MyConfig = MyConfig;

  goToFollowers() {
  this.route.navigate(['/listener/user/', this.userId, 'followers'])
  }

  goToFollowing() {
    this.route.navigate(['/listener/user/', this.userId, 'following'])

  }

  goBack(): void {
    this.location.back();
  }
}
