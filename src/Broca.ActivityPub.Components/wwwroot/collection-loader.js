export function observeSentinel(sentinelElement, dotnetRef) {
    if (!sentinelElement) return null;

    const observer = new IntersectionObserver(
        (entries) => {
            if (entries.some(e => e.isIntersecting)) {
                dotnetRef.invokeMethodAsync('OnSentinelVisible');
            }
        },
        { rootMargin: '200px' }
    );

    observer.observe(sentinelElement);
    return observer;
}

export function disconnectObserver(observer) {
    observer?.disconnect();
}
