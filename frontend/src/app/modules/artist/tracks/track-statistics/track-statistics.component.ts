import {AfterViewInit, Component, ElementRef, OnDestroy, ViewChild} from '@angular/core';
import {ActivatedRoute, Router} from '@angular/router';
import {
  CategoryScale,
  Chart,
  ChartConfiguration,
  Filler,
  Legend,
  LinearScale,
  LineController,
  LineElement,
  PointElement,
  Title,
  Tooltip
} from 'chart.js';
import {
  TrackManagementV2EndpointService,
  TrackStatisticsV2
} from '../../../../endpoints/track-endpoints/track-management-v2-endpoint.service';

Chart.register(CategoryScale, LinearScale, LineController, LineElement, PointElement, Title, Tooltip, Legend, Filler);

@Component({
  selector: 'app-track-statistics',
  templateUrl: './track-statistics.component.html',
  styleUrl: './track-statistics.component.css'
})
export class TrackStatisticsComponent implements AfterViewInit, OnDestroy {
  @ViewChild('streamsChart') streamsChart?: ElementRef<HTMLCanvasElement>;
  readonly ranges: (7 | 30 | 90 | 365)[] = [7, 30, 90, 365];
  readonly trackId: number;

  selectedDays: 7 | 30 | 90 | 365 = 30;
  statistics: TrackStatisticsV2 | null = null;
  loading = true;
  errorMessage = '';
  private chart: Chart<'line'> | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private endpoint: TrackManagementV2EndpointService
  ) {
    this.trackId = Number(this.route.snapshot.paramMap.get('trackId'));
  }

  ngAfterViewInit(): void {
    this.load(30);
  }

  ngOnDestroy(): void {
    this.chart?.destroy();
  }

  load(days: 7 | 30 | 90 | 365): void {
    this.selectedDays = days;
    this.loading = true;
    this.errorMessage = '';
    this.endpoint.getStatistics(this.trackId, days).subscribe({
      next: statistics => {
        this.statistics = statistics;
        this.loading = false;
        queueMicrotask(() => this.renderChart());
      },
      error: error => {
        this.loading = false;
        this.errorMessage = error?.error?.message ?? error?.error ?? 'Statistics could not be loaded.';
      }
    });
  }

  goBack(): void {
    this.router.navigate(['/artist/tracks', this.trackId]);
  }

  get estimatedHours(): number {
    return (this.statistics?.estimatedAllTimeStreamedSeconds ?? 0) / 3600;
  }

  private renderChart(): void {
    if (!this.streamsChart || !this.statistics) return;
    this.chart?.destroy();

    const configuration: ChartConfiguration<'line'> = {
      type: 'line',
      data: {
        labels: this.statistics.dailyStreams.map(x => new Date(x.date).toLocaleDateString(undefined, {month: 'short', day: 'numeric'})),
        datasets: [{
          label: 'Streams',
          data: this.statistics.dailyStreams.map(x => x.streams),
          borderColor: '#e692f8',
          backgroundColor: 'rgba(230, 146, 248, .16)',
          fill: true,
          tension: .32,
          pointRadius: this.selectedDays <= 30 ? 3 : 0
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {labels: {color: '#f7eff5'}},
          tooltip: {mode: 'index', intersect: false}
        },
        scales: {
          x: {ticks: {color: '#a99ea5', maxTicksLimit: 12}, grid: {color: 'rgba(255,255,255,.05)'}},
          y: {beginAtZero: true, ticks: {color: '#a99ea5', precision: 0}, grid: {color: 'rgba(255,255,255,.08)'}}
        }
      }
    };
    this.chart = new Chart(this.streamsChart.nativeElement, configuration);
  }
}
