function getScrollParent(element) {
    let parent = element.parentElement;
    while (parent && parent !== document.documentElement) {
        const style = window.getComputedStyle(parent);
        const overflow = style.overflow + style.overflowY + style.overflowX;
        if (/auto|scroll/.test(overflow) && parent.scrollHeight > parent.clientHeight) {
            return parent;
        }
        parent = parent.parentElement;
    }
    return null;
}

function isSentinelNearBottom(sentinelElement, scrollRoot) {
    const rect = sentinelElement.getBoundingClientRect();
    if (scrollRoot) {
        const rootRect = scrollRoot.getBoundingClientRect();
        return rect.top <= rootRect.bottom + 200;
    }
    return rect.top <= window.innerHeight + 200;
}

export function observeSentinel(sentinelElement, dotnetRef) {
    if (!sentinelElement) return null;

    const root = getScrollParent(sentinelElement);
    const scrollTarget = root ?? window;

    const observer = new IntersectionObserver(
        (entries) => {
            if (entries.some(e => e.isIntersecting)) {
                dotnetRef.invokeMethodAsync('OnSentinelVisible');
            }
        },
        { root, rootMargin: '200px' }
    );

    observer.observe(sentinelElement);

    const onScroll = () => {
        if (isSentinelNearBottom(sentinelElement, root)) {
            dotnetRef.invokeMethodAsync('OnSentinelVisible');
        }
    };

    scrollTarget.addEventListener('scroll', onScroll, { passive: true });

    observer._scrollTarget = scrollTarget;
    observer._onScroll = onScroll;

    return observer;
}

export function disconnectObserver(observer) {
    if (observer?._scrollTarget && observer?._onScroll) {
        observer._scrollTarget.removeEventListener('scroll', observer._onScroll);
    }
    observer?.disconnect();
}
