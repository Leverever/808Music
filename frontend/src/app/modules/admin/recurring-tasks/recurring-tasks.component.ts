import {Component, OnDestroy, OnInit} from '@angular/core';
import {HttpErrorResponse} from '@angular/common/http';
import {MatSnackBar} from '@angular/material/snack-bar';
import {catchError, of, Subject, switchMap, takeUntil, timer} from 'rxjs';
import {
  AdminRecurringTask,
  AdminRecurringTasksEndpointService
} from '../../../endpoints/admin-endpoints/admin-recurring-tasks-endpoint.service';

interface RecurringTaskPresentation {
  title: string;
  description: string;
  icon: string;
}

@Component({
  selector: 'app-recurring-tasks',
  templateUrl: './recurring-tasks.component.html',
  styleUrl: './recurring-tasks.component.css'
})
export class RecurringTasksComponent implements OnInit, OnDestroy {
  tasks: AdminRecurringTask[] = [];
  loading = true;
  errorMessage = '';
  pendingConfirmation: string | null = null;
  readonly locallyRunning = new Set<string>();
  private readonly destroy$ = new Subject<void>();

  private readonly presentations: Record<string, RecurringTaskPresentation> = {
    'audio-clustering': {
      title: 'Audio clustering',
      description: 'Queue a fresh clustering pass over analyzed audio embeddings.',
      icon: 'hub'
    },
    'daily-user-music-profile-cache': {
      title: 'User music profiles',
      description: 'Refresh daily taste profiles from recent listening and interaction signals.',
      icon: 'person_search'
    },
    'daily-automatic-playlists': {
      title: 'Daily personalized playlists',
      description: 'Generate today’s theme-driven playlists for active listeners.',
      icon: 'queue_music'
    }
  };

  constructor(
    private endpoint: AdminRecurringTasksEndpointService,
    private snackBar: MatSnackBar
  ) {
  }

  ngOnInit(): void {
    timer(0, 5000).pipe(
      switchMap(() => this.endpoint.list().pipe(
        catchError(() => {
          this.errorMessage = 'Task status could not be refreshed. Check the API connection.';
          return of(this.tasks);
        })
      )),
      takeUntil(this.destroy$)
    ).subscribe(tasks => {
      this.tasks = tasks;
      this.loading = false;
      if (tasks.length > 0) {
        this.errorMessage = '';
      }
    });
  }

  ngOnDestroy(): void {
    this.destroy$.next();
    this.destroy$.complete();
  }

  presentation(taskName: string): RecurringTaskPresentation {
    return this.presentations[taskName] ?? {
      title: taskName.replaceAll('-', ' '),
      description: 'Run this registered background task immediately.',
      icon: 'settings_suggest'
    };
  }

  isRunning(task: AdminRecurringTask): boolean {
    return task.isRunning || this.locallyRunning.has(task.name);
  }

  askToRun(task: AdminRecurringTask): void {
    if (!this.isRunning(task)) {
      this.pendingConfirmation = task.name;
    }
  }

  cancelRun(): void {
    this.pendingConfirmation = null;
  }

  run(task: AdminRecurringTask): void {
    this.pendingConfirmation = null;
    this.locallyRunning.add(task.name);

    this.endpoint.run(task.name).subscribe({
      next: result => {
        this.locallyRunning.delete(task.name);
        this.snackBar.open(
          `${this.presentation(task.name).title} completed successfully.`,
          'Dismiss',
          {duration: 4500}
        );
        task.isRunning = false;
      },
      error: (error: HttpErrorResponse) => {
        this.locallyRunning.delete(task.name);
        const message = error.status === 409
          ? `${this.presentation(task.name).title} is already running.`
          : `Could not run ${this.presentation(task.name).title.toLowerCase()}.`;
        this.snackBar.open(message, 'Dismiss', {duration: 5000});
      }
    });
  }
}
