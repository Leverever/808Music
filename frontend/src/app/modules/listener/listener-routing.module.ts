import { NgModule } from '@angular/core';
import { RouterModule, Routes } from '@angular/router';
import {AppComponent} from '../../app.component';
import {ListenerLayoutComponent} from './listener-layout/listener-layout.component';
import {NewHomeComponent} from '../public/new-home/new-home.component';
import {ReleaseViewComponent} from './release-view/release-view.component';
import {PlayTrackComponent} from './play-track/play-track.component';
import {
  SubscriptionDetailsService
} from '../../endpoints/subscription-endpoints/get-subscription-details-endpoint.service';
import {UserSubscriptionComponent} from './user-subscription/user-subscription.component';
import {PlaylistListMaterialComponent} from '../artist/playlist/playlist-list/playlist-list-material.component';
import {PlaylistCreateOrEditComponent} from '../artist/playlist/playlist-create/playlist-create-or-edit.component';
import {TracksPageComponent} from '../artist/playlist/tracks-page/tracks-page.component';
import {WebStoreComponent} from './web-store/store-home/store-home.component';
import {ProductListComponent} from '../artist/products/product-list/product-list.component';
import {ProductDetailsComponent} from '../artist/products/product-details/product-details.component';
import {ProductWishlistComponent} from './web-store/product-wishlist/product-wishlist.component';
import {StoreSearchComponent} from './web-store/store-search/store-search.component';
import {CheckoutComponent} from './web-store/checkout/checkout.component';
import {BytypeComponent} from './web-store/bytype/bytype.component';
import {ArtistPageComponent} from './artist-page/artist-page.component';
import {ArtistAlbumsListComponent} from './artist-albums-list/artist-albums-list.component';
import {NotificationsPageComponent} from './notifications-page/notifications-page.component';
import {ListenerHomeComponent} from './listener-home/listener-home.component';
import {ArtistSearchResultPageComponent} from './artist-search-result-page/artist-search-result-page.component';
import {SearchPageComponent} from '../shared/search-page/search-page.component';
import {InboxComponent} from './inbox/inbox.component';
import {SettingsComponent} from '../shared/settings/settings.component';
import {UserProfilePageComponent} from './user-profile-page/user-profile-page.component';
import {FollowersPageComponent} from './user-profile-page/follower-page/follower-page.component';
import {FollowingPageComponent} from './user-profile-page/following-page/following-page.component';
import {OrderListComponent} from './order-list/order-list.component';
import {
  OrderConfirmationDialogComponent
} from './web-store/checkout/order-confirmation-dialog/order-confirmation-dialog.component';
import {AddCollaboratorDialogComponent} from '../artist/playlist/tracks-page/add-collaborator-dialog/add-collaborator-dialog.component';
import {AddCollaboratorComponent} from '../artist/playlist/add-collaborator/add-collaborator.component';
import {UserMonthlyStatsComponent} from './user-monthly-stats/user-monthly-stats.component';
import {PersonalizedPlaylistDetailComponent} from './personalized-playlist-detail/personalized-playlist-detail.component';
import {RadioDetailComponent} from './radio-detail/radio-detail.component';
const routes: Routes = [{
  path: '', component: ListenerLayoutComponent,
  children: [
    {
      path: '', redirectTo: 'home', pathMatch: 'full',
    },
    {
      path: 'home', component: ListenerHomeComponent,
    },
    {
      path: 'user/:id', component: UserProfilePageComponent,
    },
    { path: 'playlist/collaborate', component: AddCollaboratorComponent },

    {
      path: 'release/:id', component: ReleaseViewComponent,
    },
    {
      path: 'track/:id', component: PlayTrackComponent
    },
    {
      path: 'radio/:trackId', component: RadioDetailComponent
    },
    {
      path: 'user/:id/followers', component: FollowersPageComponent
    },
    {
      path: 'user/:id/following', component: FollowingPageComponent
    },
    {
      path: 'search', component: SearchPageComponent
    },
    {
      path: 'my-orders', component: OrderListComponent
    },
    {
      path: 'profile/:id', component: ArtistPageComponent
    },
    {
      path: 'releases/:id', component: ArtistAlbumsListComponent
    },
    {
      path: 'releases', component: ArtistAlbumsListComponent
    },
    {
      path: 'notifications', component: NotificationsPageComponent
    },
    {
      path: 'chat', component: InboxComponent
    },
    {
      path: 'artists', component: ArtistSearchResultPageComponent
    },
    {
      path: 'monthly-stats', component: UserMonthlyStatsComponent
    },
    {
      path: 'settings',
      component: SettingsComponent
    },
    {
      path: 'playlist',
      children: [
        { path: '', component: PlaylistListMaterialComponent },
        { path: 'daily/:id', component: PersonalizedPlaylistDetailComponent },
        { path: 'create', component: PlaylistCreateOrEditComponent },
        { path: 'edit/:id', component: PlaylistCreateOrEditComponent },
        { path: ':id', component: TracksPageComponent }
      ]
    },
    {path: 'store-home',
      component: WebStoreComponent
    },
    {path: 'product/:slug',
      component: ProductDetailsComponent
    },
    {path: 'product-wishlist',
      component: ProductWishlistComponent
    },
    { path: 'product-search', component: StoreSearchComponent },
    { path: 'checkout', component: CheckoutComponent },
    { path: 'product-type', component: BytypeComponent },
    {path: 'subscriptions',
  component: UserSubscriptionComponent,
},
  ]},





  {
    path: 'playlist',
    component: PlaylistListMaterialComponent,
  },
];



@NgModule({
  imports: [RouterModule.forChild(routes)],
  exports: [RouterModule]
})
export class ListenerRoutingModule { }
