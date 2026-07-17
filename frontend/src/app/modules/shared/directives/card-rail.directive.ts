import {AfterViewInit, Directive, ElementRef, NgZone, OnDestroy, Renderer2} from '@angular/core';

@Directive({
  selector: '[appCardRail]'
})
export class CardRailDirective implements AfterViewInit, OnDestroy {
  private resizeObserver?: ResizeObserver;
  private mutationObserver?: MutationObserver;
  private animationFrame?: number;

  private readonly onScroll = (): void => this.scheduleUpdate();

  constructor(
    private elementRef: ElementRef<HTMLElement>,
    private renderer: Renderer2,
    private zone: NgZone
  ) {}

  ngAfterViewInit(): void {
    this.zone.runOutsideAngular(() => {
      const element = this.elementRef.nativeElement;
      element.addEventListener('scroll', this.onScroll, {passive: true});

      this.resizeObserver = new ResizeObserver(() => this.scheduleUpdate());
      this.resizeObserver.observe(element);

      this.mutationObserver = new MutationObserver(() => this.scheduleUpdate());
      this.mutationObserver.observe(element, {childList: true, subtree: true});

      this.scheduleUpdate();
    });
  }

  ngOnDestroy(): void {
    const element = this.elementRef.nativeElement;
    element.removeEventListener('scroll', this.onScroll);
    this.resizeObserver?.disconnect();
    this.mutationObserver?.disconnect();

    if(this.animationFrame !== undefined)
    {
      cancelAnimationFrame(this.animationFrame);
    }
  }

  private scheduleUpdate(): void {
    if(this.animationFrame !== undefined)
    {
      cancelAnimationFrame(this.animationFrame);
    }

    this.animationFrame = requestAnimationFrame(() => {
      this.animationFrame = undefined;
      this.updateOverflowState();
    });
  }

  private updateOverflowState(): void {
    const element = this.elementRef.nativeElement;
    const maxScrollLeft = Math.max(0, element.scrollWidth - element.clientWidth);
    const cards = Array.from(element.children).filter((child): child is HTMLElement => child instanceof HTMLElement);
    const firstCard = cards[0];
    const lastCard = cards[cards.length - 1];
    const railRect = element.getBoundingClientRect();
    const {visibleLeft, visibleRight} = this.getVisibleBounds(element, railRect);
    const canScrollBack = Boolean(firstCard && firstCard.getBoundingClientRect().left < visibleLeft - 2);
    const canScrollForward = Boolean(lastCard && lastCard.getBoundingClientRect().right > visibleRight + 2);
    const hasOverflow = maxScrollLeft > 2 || canScrollBack || canScrollForward;

    this.toggleClass('card-rail--overflowing', hasOverflow);
    this.toggleClass('card-rail--can-scroll-back', hasOverflow && canScrollBack);
    this.toggleClass('card-rail--can-scroll-forward', hasOverflow && canScrollForward);
  }

  private getVisibleBounds(element: HTMLElement, railRect: DOMRect): {
    visibleLeft: number;
    visibleRight: number;
  } {
    let visibleLeft = Math.max(railRect.left, 0);
    let visibleRight = Math.min(railRect.right, document.documentElement.clientWidth);
    let ancestor = element.parentElement;

    while(ancestor && ancestor !== document.body)
    {
      const overflowX = getComputedStyle(ancestor).overflowX;
      if(overflowX === 'auto' || overflowX === 'scroll' || overflowX === 'hidden' || overflowX === 'clip')
      {
        const ancestorRect = ancestor.getBoundingClientRect();
        visibleLeft = Math.max(visibleLeft, ancestorRect.left);
        visibleRight = Math.min(visibleRight, ancestorRect.right);
      }

      ancestor = ancestor.parentElement;
    }

    return {visibleLeft, visibleRight};
  }

  private toggleClass(className: string, enabled: boolean): void {
    if(enabled)
    {
      this.renderer.addClass(this.elementRef.nativeElement, className);
    }
    else
    {
      this.renderer.removeClass(this.elementRef.nativeElement, className);
    }
  }
}
