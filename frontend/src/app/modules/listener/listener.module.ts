  import {CUSTOM_ELEMENTS_SCHEMA, NgModule} from '@angular/core';
  import {CommonModule, NgOptimizedImage} from '@angular/common';

import { ListenerRoutingModule } from './listener-routing.module';
import { ListenerLayoutComponent } from './listener-layout/listener-layout.component';
import { MusicPlayerComponent } from './music-player/music-player.component';
import { MusicControllerComponent } from './music-player/music-controller/music-controller.component';
import {MatIcon} from '@angular/material/icon';
import {SharedModule} from '../shared/shared.module';
import {
  ClickableFeaturedArtistsComponent
} from '../shared/artist/clickable-featured-artists/clickable-featured-artists.component';
import {MatButton, MatFabButton, MatIconButton, MatMiniFabButton} from '@angular/material/button';
import {MatTooltip} from '@angular/material/tooltip';
import {MatFormField, MatInput} from "@angular/material/input";
import {MatSlider, MatSliderThumb} from '@angular/material/slider';
import { ReleaseViewComponent } from './release-view/release-view.component';
import {ReleaseTracksViewModule} from '../artist/tracks/release-tracks-view.module';
import { PlayTrackComponent } from './play-track/play-track.component';
import {MatDivider} from '@angular/material/divider';
import { ArtistPageComponent } from './artist-page/artist-page.component';
import {MatTab, MatTabContent, MatTabGroup} from '@angular/material/tabs';
import { ArtistMusicPageComponent } from './artist-page/artist-music-page/artist-music-page.component';
import { ArtistAlbumsListComponent } from './artist-albums-list/artist-albums-list.component';
import {AlbumModule} from '../artist/album/album.module';
import { NotificationsPageComponent } from './notifications-page/notifications-page.component';
import {MatChipListbox, MatChipOption} from '@angular/material/chips';
import { NotificationCardComponent } from './notifications-page/notification-card/notification-card.component';
import { ListenerHomeComponent } from './listener-home/listener-home.component';
import { ArtistSearchResultPageComponent } from './artist-search-result-page/artist-search-result-page.component';
  import {StoreArtistPageComponent} from './web-store/store-artist-page/store-artist-page.component';
  import {WebStoreComponent} from './web-store/store-home/store-home.component';
  import {UserSubscriptionComponent} from './user-subscription/user-subscription.component';
  import {ProductDetailsComponent} from '../artist/products/product-details/product-details.component';
  import {ProductWishlistComponent} from './web-store/product-wishlist/product-wishlist.component';
  import {StoreSearchComponent} from './web-store/store-search/store-search.component';
  import {CheckoutComponent} from './web-store/checkout/checkout.component';
  import {
    OrderConfirmationDialogComponent
  } from './web-store/checkout/order-confirmation-dialog/order-confirmation-dialog.component';
  import {BytypeComponent} from './web-store/bytype/bytype.component';
  import {MatAutocomplete, MatAutocompleteTrigger, MatOption} from '@angular/material/autocomplete';
  import {NgxImageZoomModule} from 'ngx-image-zoom';
import { InboxComponent } from './inbox/inbox.component';
import { ChatBoxComponent } from './inbox/chat-box/chat-box.component';
import { MessagesListComponent } from './inbox/messages-list/messages-list.component';
import { MessageCardComponent } from './inbox/message-card/message-card.component';
  import {MatStep, MatStepper} from '@angular/material/stepper';
  import {EventpageComponent} from './eventpage/eventpage.component';
  import {ImageCropperComponent} from 'ngx-image-cropper';
  import {InfiniteScrollDirective} from "ngx-infinite-scroll";
  import {MatProgressSpinner} from "@angular/material/progress-spinner";
import { UserProfilePageComponent } from './user-profile-page/user-profile-page.component';
  import {FollowersPageComponent} from './user-profile-page/follower-page/follower-page.component';
import { FollowingPageComponent } from './user-profile-page/following-page/following-page.component';
import { OrderListComponent } from './order-list/order-list.component';
  import {MatMenu, MatMenuItem, MatMenuTrigger} from "@angular/material/menu";
  import {MatCheckbox} from '@angular/material/checkbox';
  import {ArtistDialogComponent} from './artist-page/artist-dialog/artist-dialog.component';
import { UserMonthlyStatsComponent } from './user-monthly-stats/user-monthly-stats.component';
import { MonthlyStatsDialogComponent } from './monthly-stats-dialog/monthly-stats-dialog.component';
  import {MatDialogActions, MatDialogContent, MatDialogTitle} from '@angular/material/dialog';
  import {MatSlideToggle} from '@angular/material/slide-toggle';
import { PersonalizedPlaylistDetailComponent } from './personalized-playlist-detail/personalized-playlist-detail.component';
import { RadioDetailComponent } from './radio-detail/radio-detail.component';


@NgModule({
    declarations: [
        ListenerLayoutComponent,
        MusicPlayerComponent,
        MusicControllerComponent,
        ReleaseViewComponent,
        PlayTrackComponent,
        ArtistPageComponent,
        ArtistMusicPageComponent,
        ArtistAlbumsListComponent,
        NotificationsPageComponent,
        NotificationCardComponent,
        ListenerHomeComponent,
        ArtistSearchResultPageComponent,
        UserSubscriptionComponent,
        WebStoreComponent,
        StoreArtistPageComponent,
        ProductDetailsComponent,
        ProductWishlistComponent,
        StoreSearchComponent,
        CheckoutComponent,
        OrderConfirmationDialogComponent,
        BytypeComponent,
        EventpageComponent,
        InboxComponent,
        ChatBoxComponent,
        MessagesListComponent,
        MessageCardComponent,
        UserProfilePageComponent,
        FollowersPageComponent,
        FollowingPageComponent,
        OrderListComponent,
        ArtistDialogComponent,
        UserMonthlyStatsComponent,
        MonthlyStatsDialogComponent,
        PersonalizedPlaylistDetailComponent,
        RadioDetailComponent,
    ],
  imports: [
    CommonModule,
    ListenerRoutingModule,
    MatIcon,
    SharedModule,
    MatIconButton,
    MatTooltip,
    MatInput,
    MatFormField,
    MatButton,
    MatSlider,
    MatSliderThumb,
    ReleaseTracksViewModule,
    MatDivider,
    MatFabButton,
    MatTabGroup,
    MatTab,
    AlbumModule,
    MatChipListbox,
    MatChipOption,
    MatAutocomplete,
    MatOption,
    MatAutocompleteTrigger,
    NgxImageZoomModule,
    MatStepper,
    MatStep,
    MatMiniFabButton,
    ImageCropperComponent,
    MatTabContent,
    InfiniteScrollDirective,
    MatProgressSpinner,
    MatMenu,
    MatMenuItem,
    MatMenuTrigger,
    MatCheckbox,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatSlideToggle,
  ],
    exports: [
        ListenerHomeComponent
    ],
    schemas: [CUSTOM_ELEMENTS_SCHEMA]
})
export class ListenerModule { }



