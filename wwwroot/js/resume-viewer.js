import * as pdfjsLib from '/lib/pdfjs/pdf.min.mjs';

pdfjsLib.GlobalWorkerOptions.workerSrc = '/lib/pdfjs/pdf.worker.min.mjs';

document.addEventListener('DOMContentLoaded', () => {
    const wrapper = document.getElementById('resumeCanvasWrapper');
    const canvas = document.getElementById('resumeCanvas');
    const loadingEl = document.getElementById('resumeLoading');
    if (!wrapper || !canvas) return;

    let pdfDoc = null;

    // The resume is a single page today; if it ever grows to multiple pages,
    // loop over doc.numPages and stack one <canvas> per page instead.
    function renderPage() {
        pdfDoc.getPage(1).then(page => {
            const containerWidth = wrapper.clientWidth;
            const unscaledViewport = page.getViewport({ scale: 1 });
            const scale = containerWidth / unscaledViewport.width;
            const viewport = page.getViewport({ scale });

            const dpr = window.devicePixelRatio || 1;
            const ctx = canvas.getContext('2d');
            canvas.width = viewport.width * dpr;
            canvas.height = viewport.height * dpr;
            canvas.style.width = `${viewport.width}px`;
            canvas.style.height = `${viewport.height}px`;
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);

            page.render({ canvasContext: ctx, viewport });
        });
    }

    pdfjsLib.getDocument('/shared/docs/resume.pdf').promise
        .then(doc => {
            pdfDoc = doc;
            loadingEl.style.display = 'none';
            renderPage();
        })
        .catch(() => {
            loadingEl.textContent = 'Preview unavailable — please use the download button above.';
        });

    let resizeTimer;
    window.addEventListener('resize', () => {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(() => {
            if (pdfDoc) renderPage();
        }, 200);
    });
});
