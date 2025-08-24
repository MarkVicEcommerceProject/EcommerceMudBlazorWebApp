// Counter animation function

console.log("aboutPage.js loaded");
export function animateCounters() {
    const counters = document.querySelectorAll('.counter');
    const speed = 200;

    counters.forEach(counter => {
        const updateCount = () => {
            const target = +counter.getAttribute('data-target');
            const count = +counter.innerText;
            const increment = target / speed;

            if (count < target) {
                counter.innerText = Math.ceil(count + increment);
                setTimeout(updateCount, 1);
            } else {
                counter.innerText = target.toLocaleString();
            }
        };
        updateCount();
    });
}

export function initAnimations() {
    const observer = new IntersectionObserver((entries) => {
        entries.forEach(entry => {
            if (!entry.isIntersecting) return;

            const el = entry.target;
            const animation = el.dataset.animate;

            // remove starting hidden class to avoid conflict
            if (el.classList.contains('opacity-0')) {
                el.classList.remove('opacity-0');
            }

            el.classList.add('opacity-100');

            // optional per-element delay
            const delay = parseFloat(el.dataset.delay) || 0; // seconds

            // force reflow then add animation class after delay
            void el.offsetWidth; // force reflow
            if (delay > 0) {
                setTimeout(() => el.classList.add(animation), delay * 1000);
            } else {
                el.classList.add(animation);
            }

            observer.unobserve(el);
        });
    }, { threshold: 0.5 });

    document.querySelectorAll('[data-animate]').forEach(el => observer.observe(el));
}
