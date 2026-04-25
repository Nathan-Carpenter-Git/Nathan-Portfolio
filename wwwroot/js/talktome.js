// talktome.js — chat page logic for TalkToMe view

// ── State ────────────────────────────────────────────────
// Each entry: { role: "user"|"assistant", content: "..." }
let messageHistory = [];
let isWaiting = false;

// ── Element refs ─────────────────────────────────────────
const chatMessages = document.getElementById('chatMessages');
const chatInput = document.getElementById('chatInput');
const sendBtn = document.getElementById('sendBtn');
const emptyState = document.getElementById('emptyState');

// ── Clear chat ────────────────────────────────────────────
document.getElementById('btnClear').addEventListener('click', () => {
    if (messageHistory.length === 0) return;
    messageHistory = [];
    chatMessages.innerHTML = '';
    chatMessages.appendChild(emptyState);
    emptyState.style.display = '';
});

// ── Auto-resize textarea ──────────────────────────────────
chatInput.addEventListener('input', () => {
    chatInput.style.height = '0';
    chatInput.style.height = Math.min(chatInput.scrollHeight, 150) + 'px';
    sendBtn.disabled = chatInput.value.trim() === '' || isWaiting;
});

// ── Keyboard shortcuts ────────────────────────────────────
chatInput.addEventListener('keydown', (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
        e.preventDefault();
        if (!sendBtn.disabled) sendMessage();
    }
});

sendBtn.addEventListener('click', sendMessage);

// ── Render a message bubble ───────────────────────────────
function renderMessage(role, content, isTyping = false) {
    emptyState.style.display = 'none';

    const row = document.createElement('div');
    row.classList.add('chat-message', role);

    const avatar = document.createElement('div');
    avatar.classList.add('chat-avatar');
    avatar.textContent = role === 'user' ? 'NC' : 'AI';

    const bubble = document.createElement('div');
    bubble.classList.add('chat-bubble');

    if (isTyping) {
        bubble.innerHTML = `<div class="chat-typing">
            <span></span><span></span><span></span>
        </div>`;
        row.id = 'typingIndicator';
    } else {
        bubble.textContent = content;
    }

    row.appendChild(avatar);
    row.appendChild(bubble);
    chatMessages.appendChild(row);
    chatMessages.scrollTop = chatMessages.scrollHeight;
    return row;
}

// ── Send message ──────────────────────────────────────────
async function sendMessage() {
    const text = chatInput.value.trim();
    if (!text || isWaiting) return;

    // Reset input
    chatInput.value = '';
    chatInput.style.height = '';
    sendBtn.disabled = true;
    isWaiting = true;

    // Add user message to history and UI
    messageHistory.push({ role: 'user', content: text });
    renderMessage('user', text);

    // Show typing indicator
    renderMessage('assistant', '', true);

    try {
        const res = await fetch('/TalkToMe/SendMessage', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({
                message: text,
                history: messageHistory.slice(0, -1)
            })
        });

        const data = await res.json();

        // Remove typing indicator
        document.getElementById('typingIndicator')?.remove();

        if (!res.ok || data.error) {
            const errRow = renderMessage('assistant', 'Error: ' + (data.error ?? 'Something went wrong.'));
            errRow.querySelector('.chat-bubble').classList.add('error');
        } else {
            messageHistory.push({ role: 'assistant', content: data.reply });
            renderMessage('assistant', data.reply);
        }

    } catch (err) {
        document.getElementById('typingIndicator')?.remove();
        const errRow = renderMessage('assistant', 'Network error — please try again.');
        errRow.querySelector('.chat-bubble').classList.add('error');
        // Roll back the user message that failed
        messageHistory.pop();
    }

    isWaiting = false;
    sendBtn.disabled = chatInput.value.trim() === '';
    chatInput.focus();
}