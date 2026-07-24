import { Component, OnInit } from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {MyConfig} from '../../../../my-config';
import {UserFollowService} from '../../../../endpoints/user-endpoints/get-user-followage-endpoint.service';
import { Location } from '@angular/common';
import {animate, style, transition, trigger} from '@angular/animations';
import {UserHeaderColorService} from '../../../../endpoints/user-endpoints/user-header-color-endpoint.service';

@Component({
  selector: 'app-followers-page',
  templateUrl: './follower-page.component.html',
  styleUrls: ['../followage-page.css'],
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
export class FollowersPageComponent implements OnInit {
  followers: any[] = [];
  userId: number = 0;
  selectedColor = '#e692f8';
  readonly mediaAddress = MyConfig.media_address;

  constructor(
    private route: ActivatedRoute,
    private userFollowService: UserFollowService,
    private location : Location,
    private router : Router,
    private userHeaderColorService: UserHeaderColorService,
  ) {}

  ngOnInit(): void {
    this.userId = Number(this.route.snapshot.params['id']);
    this.userHeaderColorService.getHeaderColor(this.userId).subscribe({
      next: response => this.selectedColor = response.headerColor || this.selectedColor,
      error: err => console.error('Error fetching header color:', err),
    });
    this.loadFollowers();
  }

  loadFollowers(): void {
    this.userFollowService.getFollowingAndFollowers(this.userId).subscribe({
      next: (response) => {
        this.followers = response.followers;
      },
      error: (err) => {
        console.error('Error fetching followers:', err);
      },
    });
  }

  openProfile(userId: number): void {
    console.log(userId);
    this.router.navigate(['listener/user/', userId]);
  }

  goBack() {
    this.location.back();
  }
}
