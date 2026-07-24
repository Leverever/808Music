import {AfterViewInit, Directive, ElementRef, NgZone, OnDestroy} from '@angular/core';

@Directive({
  selector: '[appMobileDetailScroll]'
})
export class MobileDetailScrollDirective implements AfterViewInit, OnDestroy {
  private static readonly MAX_SCALE_REDUCTION = 0.24;
  private static readonly MAX_UPWARD_SHIFT = 18;

  private scrollContainer: HTMLElement | null = null;
  private animationFrame: number | null = null;
  private contentObserver: MutationObserver | null = null;
  private readonly onScroll = () => this.scheduleUpdate();
  private readonly onResize = () => this.scheduleUpdate();

  constructor(
    private readonly elementRef: ElementRef<HTMLElement>,
    private readonly ngZone: NgZone
  ) {}

  ngAfterViewInit(): void {
    this.scrollContainer = this.findScrollContainer();

    this.ngZone.runOutsideAngular(() => {
      this.scrollContainer?.addEventListener('scroll', this.onScroll, {passive: true});
      window.addEventListener('resize', this.onResize, {passive: true});
      this.contentObserver = new MutationObserver(() => this.scheduleUpdate());
      this.contentObserver.observe(this.elementRef.nativeElement, {
        childList: true,
        subtree: true
      });
      this.scheduleUpdate();
    });
  }

  ngOnDestroy(): void {
    this.scrollContainer?.removeEventListener('scroll', this.onScroll);
    window.removeEventListener('resize', this.onResize);
    this.contentObserver?.disconnect();

    if(this.animationFrame != null)
    {
      cancelAnimationFrame(this.animationFrame);
    }
  }

  private findScrollContainer(): HTMLElement | null {
    let parent = this.elementRef.nativeElement.parentElement;

    while(parent)
    {
      if(parent.classList.contains('routed-content'))
      {
        return parent;
      }

      const overflowY = getComputedStyle(parent).overflowY;
      if(overflowY === 'auto' || overflowY === 'scroll' || overflowY === 'overlay')
      {
        return parent;
      }

      parent = parent.parentElement;
    }

    return null;
  }

  private scheduleUpdate(): void {
    if(this.animationFrame != null)
    {
      return;
    }

    this.animationFrame = requestAnimationFrame(() => {
      this.animationFrame = null;
      this.updateProgress();
    });
  }

  private updateProgress(): void {
    const scrollTop = this.scrollContainer?.scrollTop ?? window.scrollY;
    const collapseDistance = Math.min(240, Math.max(160, window.innerHeight * 0.24));
    const progress = Math.min(1, Math.max(0, scrollTop / collapseDistance));
    const artwork = this.elementRef.nativeElement.querySelector<HTMLElement>('.detail-artwork');
    const artworkHeight = artwork?.offsetHeight ?? 0;
    const scaleReduction = MobileDetailScrollDirective.MAX_SCALE_REDUCTION * progress;
    const upwardShift = MobileDetailScrollDirective.MAX_UPWARD_SHIFT * progress;
    const reclaimedSpace = artworkHeight * scaleReduction + upwardShift;
    const hostStyle = this.elementRef.nativeElement.style;

    hostStyle.setProperty('--detail-collapse-progress', progress.toFixed(3));
    hostStyle.setProperty('--detail-artwork-scale', (1 - scaleReduction).toFixed(3));
    hostStyle.setProperty('--detail-artwork-shift', `${Math.round(-upwardShift)}px`);
    hostStyle.setProperty('--detail-artwork-reclaim', `${Math.round(-reclaimedSpace)}px`);
  }
}
