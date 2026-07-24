import {Component, ElementRef, OnDestroy, ViewChild} from '@angular/core';
import {
  EventGetByArtistIdService, ArtistEvents
} from '../../../endpoints/user-artist-endpoints/get-events-by-artist-endpoint.service';
import {ActivatedRoute} from '@angular/router';
import {MyConfig} from '../../../my-config';
import {ArtistGetByIdEndpointService} from '../../../endpoints/artist-endpoints/artist-get-by-id-endpoint.service';
import {HttpErrorResponse} from '@angular/common/http';
import moment from 'moment';
import * as L from 'leaflet';
import {animate, style, transition, trigger} from '@angular/animations';

@Component({
  selector: 'app-eventpage',
  templateUrl: './eventpage.component.html',
  styleUrl: './eventpage.component.css',
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
export class EventpageComponent implements OnDestroy {
  @ViewChild('eventMap') mapElement?: ElementRef<HTMLElement>;

  ArtistEvents: ArtistEvents[] = [];
  selectedEvent: ArtistEvents | null = null;
  artistData: any | null = null;
  mapInstance: L.Map | null = null;
  isMapLoading = false;
  private mapInitializationTimeout?: ReturnType<typeof setTimeout>;
  private mapAnimationFrame?: number;
  private readonly mapInitializationDelay = 2500;

  constructor(private eventService: EventGetByArtistIdService, private route :ActivatedRoute, private artistGetByIdEndpointService :ArtistGetByIdEndpointService) {}

  ngOnInit(): void {
    this.route.params.subscribe(params => {
      let artistId = params['id'];
      this.eventService.getEventsByArtist(artistId).subscribe(
        (data) => {
          this.ArtistEvents = data;
          this.selectClosestEvent();
        },
        (error) => {
          console.error('Error fetching events:', error);
        }
      );})

    this.route.params.subscribe(params => {
      let artID = params['id'];
      this.artistGetByIdEndpointService.handleAsync(artID as number).subscribe(
        (data: any) => {
          this.artistData = data;
        },
        (error: HttpErrorResponse) => {
          console.error('Error fetching events:', error);
        }
      );
    })
  }

  selectClosestEvent() {
    if (this.ArtistEvents && this.ArtistEvents.length > 0) {
      const now = new Date();
      this.selectedEvent = this.ArtistEvents
        .map((event) => ({
          ...event,
          timeDiff: Math.abs(new Date(event.eventDate).getTime() - now.getTime())
        }))
        .sort((a, b) => a.timeDiff - b.timeDiff)[0];
    }
    if (this.selectedEvent) {
      this.scheduleMapInitialization(this.selectedEvent);
    }
  }

  selectEvent(event: ArtistEvents) {
    this.selectedEvent = event;
    this.scheduleMapInitialization(event);
  }

  initMap(event: ArtistEvents) {
    const mapElement = this.mapElement?.nativeElement;

    if(!mapElement)
    {
      return;
    }

    if (this.mapInstance) {
      this.mapInstance.remove();
    }

    const customIcon = L.icon({
      iconUrl: 'https://files.cults3d.com/uploaders/23511406/illustration-file/9aafa4e5-8d7b-479d-becc-79bc235b065f/379683535646300a3a95677589335670.png',
      iconSize: [32, 32],
      iconAnchor: [16, 32],
      popupAnchor: [0, -32]
    });

    const map = L.map(mapElement).setView([event.latitude, event.longitude], 10);

    L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
      attribution: '&copy; <a href="http://www.openstreetmap.org/copyright">OpenStreetMap</a>'
    }).addTo(map);

    L.marker([event.latitude, event.longitude], { icon: customIcon }).addTo(map)
      .bindPopup(`<b>${event.eventTitle}</b><br>${event.city}, ${event.country}`)
      .openPopup();

    this.mapInstance = map;
    this.isMapLoading = false;
    requestAnimationFrame(() => map.invalidateSize());
  }

  ngOnDestroy(): void {
    if(this.mapInitializationTimeout !== undefined)
    {
      clearTimeout(this.mapInitializationTimeout);
    }

    if(this.mapAnimationFrame !== undefined)
    {
      cancelAnimationFrame(this.mapAnimationFrame);
    }

    this.mapInstance?.remove();
    this.mapInstance = null;
  }

  private scheduleMapInitialization(event: ArtistEvents): void {
    if(this.mapInitializationTimeout !== undefined)
    {
      clearTimeout(this.mapInitializationTimeout);
      this.mapInitializationTimeout = undefined;
    }

    if(this.mapAnimationFrame !== undefined)
    {
      cancelAnimationFrame(this.mapAnimationFrame);
      this.mapAnimationFrame = undefined;
    }

    this.mapInstance?.remove();
    this.mapInstance = null;
    this.isMapLoading = true;

    this.mapInitializationTimeout = setTimeout(() => {
      this.mapInitializationTimeout = undefined;
      this.mapAnimationFrame = requestAnimationFrame(() => {
        this.mapAnimationFrame = undefined;
        this.initMap(event);
      });
    }, this.mapInitializationDelay);
  }

  protected readonly MyConfig = MyConfig;
  protected readonly moment = moment;
}
