import {Component, OnInit} from '@angular/core';
import {forkJoin} from 'rxjs';
import {
  AdminRecurringTasksEndpointService
} from '../../../endpoints/admin-endpoints/admin-recurring-tasks-endpoint.service';
import {
  AdminPlaylistThemesEndpointService
} from '../../../endpoints/admin-endpoints/admin-playlist-themes-endpoint.service';

@Component({
  selector: 'app-dashboard',
  templateUrl: './dashboard.component.html',
  styleUrl: './dashboard.component.css'
})
export class DashboardComponent implements OnInit {
  loading = true;
  scheduledTaskCount = 0;
  runningTaskCount = 0;
  activeThemeCount = 0;
  totalThemeCount = 0;

  constructor(
    private recurringTasks: AdminRecurringTasksEndpointService,
    private playlistThemes: AdminPlaylistThemesEndpointService
  ) {
  }

  ngOnInit(): void {
    forkJoin({
      tasks: this.recurringTasks.list(),
      themes: this.playlistThemes.list()
    }).subscribe({
      next: ({tasks, themes}) => {
        this.scheduledTaskCount = tasks.filter(task => task.isScheduled).length;
        this.runningTaskCount = tasks.filter(task => task.isRunning).length;
        this.activeThemeCount = themes.filter(theme => theme.isActive).length;
        this.totalThemeCount = themes.length;
        this.loading = false;
      },
      error: () => {
        this.loading = false;
      }
    });
  }
}
