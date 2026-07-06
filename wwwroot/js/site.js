document.body.style.opacity = '0';
window.addEventListener('DOMContentLoaded', () => {
    document.body.style.transition = 'opacity 1.5s ease-in';
    requestAnimationFrame(() => { document.body.style.opacity = '1'; });
});
