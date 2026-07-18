import { ChangeDetectorRef, Component, OnInit } from '@angular/core';
import { loadStripe, Stripe, StripeCardElement, StripeElements } from '@stripe/stripe-js';
import { MatSnackBar } from '@angular/material/snack-bar';
import { SubscriptionDetails, SubscriptionDetailsService } from '../../../endpoints/subscription-endpoints/get-subscription-details-endpoint.service';
import { SubscriptionAddEndpointService } from '../../../endpoints/subscription-endpoints/add-subscription-endpoint.service';
import { StripeService } from '../../../endpoints/stripe-endpoints/stripe-endpoint.service';
import { UserService } from '../../../endpoints/user-endpoints/get-user-info-endpoints.service';
import { UserSubscriptionDetailsResponse, UserSubscriptionService } from '../../../endpoints/subscription-endpoints/user-subscription-type-endpoint.service';

@Component({
  selector: 'app-user-subscription',
  templateUrl: './user-subscription.component.html',
  styleUrls: ['./user-subscription.component.css'],
})
export class UserSubscriptionComponent implements OnInit {
  subscriptions: SubscriptionDetails[] = [];
  subscriptionDetails: UserSubscriptionDetailsResponse = {
    subscription: null,
  };
  selectedSubscription: SubscriptionDetails | null = null;
  stripe: Stripe | null = null;
  elements: StripeElements | null = null;
  card: StripeCardElement | null = null;
  userSubscribed = false;
  private plansLoading = true;
  private currentSubscriptionLoading = true;
  processing = false;
  paymentSuccess = false;
  errorMessage = '';
  readonly planColors: Record<number, string> = { 1: '#b3f6b3', 2: '#f6e795', 3: '#cf9ef8' };

  get loading(): boolean {
    return this.plansLoading || this.currentSubscriptionLoading;
  }

  constructor(
    private readonly subscriptionService: SubscriptionDetailsService,
    private readonly subscriptionAdd: SubscriptionAddEndpointService,
    private readonly userService: UserService,
    private readonly stripeService: StripeService,
    private readonly userSubscription: UserSubscriptionService,
    private readonly snackBar: MatSnackBar,
    private readonly cdr: ChangeDetectorRef,
  ) {}

  async ngOnInit(): Promise<void> {
    const userId = this.getUserId();
    if (!userId) {
      this.plansLoading = false;
      this.currentSubscriptionLoading = false;
      this.errorMessage = 'Sign in to manage a subscription.';
      return;
    }

    this.loadCurrentSubscription(userId);
    this.subscriptionService.getAll().subscribe({
      next: plans => {
        this.subscriptions = plans ?? [];
        this.plansLoading = false;
      },
      error: () => {
        this.errorMessage = 'Subscription plans could not be loaded.';
        this.plansLoading = false;
      },
    });

    this.stripe = await loadStripe('pk_test_51QEZojCQgR3U8MdBa1uGUBRSshgia3TauM5hIFtla1wprW3iNEJX6yzk1p2liFGNmjavOYxRDxDEvauXP7in5gOZ00Jr5eCt3w');
    if (this.stripe) {
      this.elements = this.stripe.elements();
      if (this.selectedSubscription) this.mountCard();
    }
  }

  selectPlan(subscription: SubscriptionDetails): void {
    this.selectedSubscription = subscription;
    this.paymentSuccess = false;
    this.errorMessage = '';
    this.cdr.detectChanges();
    setTimeout(() => this.mountCard());
  }

  handleSubmit(): void {
    const userId = this.getUserId();
    if (!userId || !this.selectedSubscription || !this.stripe || !this.card) {
      this.errorMessage = 'Select a plan and wait for payment details to load.';
      return;
    }

    this.processing = true;
    this.userService.getUserInfo(userId).subscribe({
      next: user => this.stripeService.createPaymentIntent(Math.round(this.totalPrice(this.selectedSubscription!) * 100), user.email).subscribe({
        next: response => this.confirmPayment(response.clientSecret, userId),
        error: error => this.failPayment('Could not start payment: ' + error.message),
      }),
      error: error => this.failPayment('Could not load billing information: ' + error.message),
    });
  }

  totalPrice(plan: SubscriptionDetails): number {
    return plan.price * this.planMonths(plan);
  }

  planMonths(plan: SubscriptionDetails): number {
    if (plan.subscriptionType === 2) return 6;
    if (plan.subscriptionType === 3) return 12;
    return 1;
  }

  planCadence(plan: SubscriptionDetails): string {
    const months = this.planMonths(plan);
    return months === 1 ? 'Billed monthly' : `Billed every ${months} months`;
  }

  planIcon(plan: SubscriptionDetails): string {
    return plan.subscriptionType === 3 ? 'workspace_premium' : plan.subscriptionType === 2 ? 'auto_awesome' : 'music_note';
  }

  planColor(plan: SubscriptionDetails): string {
    return this.planColors[plan.subscriptionType] ?? '#e692f8';
  }

  private mountCard(): void {
    const target = document.getElementById('subscription-card-element');
    if (!target || !this.elements) return;
    if (this.card) this.card.unmount();
    this.card = this.elements.create('card', {
      style: {
        base: { color: '#ffffff', fontSize: '16px', iconColor: '#e692f8', '::placeholder': { color: 'rgba(243,233,244,.52)' } },
        invalid: { color: '#ff9fb1', iconColor: '#ff9fb1' },
      },
    });
    this.card.mount(target);
  }

  private confirmPayment(clientSecret: string, userId: number): void {
    this.stripe!.confirmCardPayment(clientSecret, { payment_method: { card: this.card! } }).then(result => {
      if (result.error) {
        this.failPayment(result.error.message || 'Payment failed.');
      } else if (result.paymentIntent?.status === 'succeeded') {
        this.addSubscription(userId);
      }
    });
  }

  private addSubscription(userId: number): void {
    this.subscriptionAdd.handleAsync({ userId, subscriptionType: this.selectedSubscription?.id || 1, renewalOn: true }).subscribe({
      next: response => {
        if (!response.success) {
          this.failPayment(response.message);
          return;
        }
        this.processing = false;
        this.paymentSuccess = true;
        this.snackBar.open('Subscription activated', 'Close', { duration: 1800 });
        this.loadCurrentSubscription(userId);
      },
      error: error => this.failPayment('Could not activate subscription: ' + error.message),
    });
  }

  private loadCurrentSubscription(userId: number): void {
    this.currentSubscriptionLoading = true;
    this.userSubscription.getUserSubscriptionDetails(userId).subscribe({
      next: response => {
        this.subscriptionDetails = response;
        this.userSubscribed = Number(response.subscription?.subscriptionType ?? 0) > 0;
        this.currentSubscriptionLoading = false;
      },
      error: () => {
        this.subscriptionDetails = { subscription: null };
        this.userSubscribed = false;
        this.currentSubscriptionLoading = false;
      },
    });
  }

  private failPayment(message: string): void {
    this.processing = false;
    this.paymentSuccess = false;
    this.errorMessage = message;
  }

  private getUserId(): number {
    const token = sessionStorage.getItem('authToken') ?? localStorage.getItem('authToken');
    if (!token) return 0;
    try { return Number(JSON.parse(token).userId) || 0; } catch { return 0; }
  }
}
