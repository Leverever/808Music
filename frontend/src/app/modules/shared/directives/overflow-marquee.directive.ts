import {
  AfterViewInit,
  booleanAttribute,
  Directive,
  ElementRef,
  Input,
  OnChanges,
  OnDestroy,
  SimpleChanges
} from '@angular/core';

@Directive({
  selector: '[appOverflowMarquee]',
  standalone: true
})
export class OverflowMarqueeDirective implements AfterViewInit, OnChanges, OnDestroy {
  @Input({transform: booleanAttribute}) appOverflowMarquee = true;

  private animation: Animation | null = null;
  private resizeObserver: ResizeObserver | null = null;
  private mutationObserver: MutationObserver | null = null;
  private animationFrame: number | null = null;
  private initialized = false;

  constructor(private elementRef: ElementRef<HTMLElement>) {
  }

  ngAfterViewInit(): void {
    const element = this.elementRef.nativeElement;
    element.style.display = 'block';
    element.style.width = 'max-content';
    element.style.maxWidth = 'none';
    element.style.whiteSpace = 'nowrap';

    this.resizeObserver = new ResizeObserver(() => this.scheduleRefresh());
    this.resizeObserver.observe(element);
    if(element.parentElement != null)
    {
      this.resizeObserver.observe(element.parentElement);
    }

    this.mutationObserver = new MutationObserver(() => this.scheduleRefresh());
    this.mutationObserver.observe(element, {
      childList: true,
      characterData: true,
      subtree: true
    });

    this.initialized = true;
    this.scheduleRefresh();
    void document.fonts?.ready.then(() => this.scheduleRefresh());
  }

  ngOnChanges(changes: SimpleChanges): void {
    if(this.initialized && changes['appOverflowMarquee'] != null)
    {
      this.scheduleRefresh();
    }
  }

  ngOnDestroy(): void {
    this.animation?.cancel();
    this.resizeObserver?.disconnect();
    this.mutationObserver?.disconnect();
    if(this.animationFrame != null)
    {
      window.cancelAnimationFrame(this.animationFrame);
    }
  }

  private scheduleRefresh(): void {
    if(this.animationFrame != null)
    {
      window.cancelAnimationFrame(this.animationFrame);
    }

    this.animationFrame = window.requestAnimationFrame(() => {
      this.animationFrame = null;
      this.refreshAnimation();
    });
  }

  private refreshAnimation(): void {
    const element = this.elementRef.nativeElement;
    const viewport = element.parentElement;
    this.animation?.cancel();
    this.animation = null;
    element.style.transform = 'translate3d(0, 0, 0)';

    if(
      !this.appOverflowMarquee ||
      viewport == null ||
      viewport.clientWidth <= 0 ||
      window.matchMedia('(prefers-reduced-motion: reduce)').matches
    )
    {
      return;
    }

    const travelDistance = Math.ceil(element.scrollWidth - viewport.clientWidth);
    if(travelDistance <= 2)
    {
      return;
    }

    const duration = Math.min(14000, Math.max(6000, 3600 + travelDistance * 32));
    this.animation = element.animate(
      [
        {transform: 'translate3d(0, 0, 0)', offset: 0},
        {transform: 'translate3d(0, 0, 0)', offset: .16},
        {transform: `translate3d(-${travelDistance}px, 0, 0)`, offset: .84},
        {transform: `translate3d(-${travelDistance}px, 0, 0)`, offset: 1}
      ],
      {
        duration,
        easing: 'linear',
        iterations: Infinity,
        direction: 'alternate'
      }
    );
  }
}
