﻿
function openInNewTab(url) {
    window.open(url, '_blank');
}

function scrollToTop() {
    document.documentElement.scrollTop = 0;
}

function getClipboardText() {
    return navigator.clipboard.readText();
}

async function downloadFileFromStream(fileName, contentStreamReference) {
    const arrayBuffer = await contentStreamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer]);
    const url = URL.createObjectURL(blob);
    const anchorElement = document.createElement('a');
    anchorElement.href = url;
    anchorElement.download = fileName ?? '';
    anchorElement.click();
    anchorElement.remove();
    URL.revokeObjectURL(url);
}
