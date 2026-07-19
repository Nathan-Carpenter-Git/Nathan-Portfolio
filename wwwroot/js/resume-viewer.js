// Dynamically imported by home.js once the résumé section nears the
// viewport, so the DOM is already ready by the time this module runs -
// no need to wait on DOMContentLoaded here.
import * as pdfjsLib from '/lib/pdfjs/pdf.min.mjs';

pdfjsLib.GlobalWorkerOptions.workerSrc = '/lib/pdfjs/pdf.worker.min.mjs';

const wrapper = document.getElementById('resumeCanvasWrapper');
const canvas = document.getElementById('resumeCanvas');
const loadingEl = document.getElementById('resumeLoading');

if (wrapper && canvas) {
    let pdfDoc = null;
    let currentRenderTask = null;

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

            if (currentRenderTask) {
                currentRenderTask.cancel();
            }
            currentRenderTask = page.render({ canvasContext: ctx, viewport });
            currentRenderTask.promise.catch(err => {
                if (err && err.name !== 'RenderingCancelledException') throw err;
            });
        });
    }

    const resumeUrl = wrapper.dataset.resumeUrl || '/shared/docs/resume.pdf';
    pdfjsLib.getDocument(resumeUrl).promise
        .then(doc => {
            pdfDoc = doc;
            loadingEl.style.display = 'none';
            renderPage();
        })
        .catch(() => {
            loadingEl.textContent = 'Preview unavailable. Please use the download button above.';
        });

    let resizeTimer;
    window.addEventListener('resize', () => {
        clearTimeout(resizeTimer);
        resizeTimer = setTimeout(() => {
            if (pdfDoc) renderPage();
        }, 200);
    });
}
