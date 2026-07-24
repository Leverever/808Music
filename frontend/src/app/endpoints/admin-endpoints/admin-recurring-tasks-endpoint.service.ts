import {HttpClient} from '@angular/common/http';
import {Injectable} from '@angular/core';
import {Observable} from 'rxjs';
import {MyConfig} from '../../my-config';

export interface AdminRecurringTask {
  name: string;
  cronExpression: string;
  isScheduled: boolean;
  isRunning: boolean;
  nextScheduledRunUtc: string | null;
}

export interface AdminRecurringTaskRunResult {
  name: string;
  startedAt: string;
  completedAt: string;
  status: string;
}

@Injectable({providedIn: 'root'})
export class AdminRecurringTasksEndpointService {
  private readonly url = `${MyConfig.api_address}/api/v2/admin/recurring-tasks`;

  constructor(private http: HttpClient) {
  }

  list(): Observable<AdminRecurringTask[]> {
    return this.http.get<AdminRecurringTask[]>(this.url);
  }

  run(taskName: string): Observable<AdminRecurringTaskRunResult> {
    return this.http.post<AdminRecurringTaskRunResult>(
      `${this.url}/${encodeURIComponent(taskName)}/run`,
      null
    );
  }
}
