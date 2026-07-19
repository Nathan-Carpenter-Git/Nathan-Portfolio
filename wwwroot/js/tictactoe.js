// tictactoe.js - board logic for the TicTacToe view

const WIN_LINES = [
    [0, 1, 2], [3, 4, 5], [6, 7, 8],
    [0, 3, 6], [1, 4, 7], [2, 5, 8],
    [0, 4, 8], [2, 4, 6]
];

const boardEl = document.getElementById('tttBoard');
const statusEl = document.getElementById('tttStatus');
const cells = Array.from(boardEl.querySelectorAll('.ttt-cell'));

let board = Array(9).fill(null);
let humanMark = 'X';
let aiMark = 'O';
let gameOver = false;
let aiThinking = false;

cells.forEach(cell => {
    cell.addEventListener('click', () => onCellClick(Number(cell.dataset.index)));
});

document.getElementById('tttNewGame').addEventListener('click', startNewGame);

startNewGame();

// ── Game flow ────────────────────────────────────────────

function startNewGame() {
    board = Array(9).fill(null);
    gameOver = false;
    aiThinking = false;
    humanMark = Math.random() < 0.5 ? 'X' : 'O';
    aiMark = humanMark === 'X' ? 'O' : 'X';

    cells.forEach((cell, i) => {
        cell.textContent = '';
        cell.className = 'ttt-cell';
        cell.disabled = false;
        cell.setAttribute('aria-label', `Cell ${i + 1} of 9, empty`);
    });

    // X always moves first - if the AI is X, it opens.
    if (aiMark === 'X') {
        setStatus(`You're ${humanMark}. AI opens as X...`);
        requestAiMove();
    } else {
        setStatus(`You're ${humanMark}. Your move.`);
    }
}

function onCellClick(index) {
    if (gameOver || aiThinking || board[index] !== null) return;
    if (currentTurnMark() !== humanMark) return;

    placeMark(index, humanMark);
    if (!checkGameEnd()) {
        setStatus('AI is thinking...');
        requestAiMove();
    }
}

async function requestAiMove() {
    aiThinking = true;

    try {
        const res = await fetch('/TicTacToe/Move', {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ board, aiMark })
        });

        const data = await res.json();

        if (!res.ok || typeof data.index !== 'number' || board[data.index] !== null) {
            setStatus("The AI couldn't move - try New Game.");
            aiThinking = false;
            return;
        }

        placeMark(data.index, aiMark);
        aiThinking = false;

        if (!checkGameEnd()) {
            setStatus('Your move.');
        }
    } catch (err) {
        setStatus('Network error - try New Game.');
        aiThinking = false;
    }
}

// ── Board helpers ────────────────────────────────────────

function currentTurnMark() {
    const filled = board.filter(Boolean).length;
    return filled % 2 === 0 ? 'X' : 'O';
}

function placeMark(index, mark) {
    board[index] = mark;
    const cell = cells[index];
    // O is drawn as a CSS circle (see .ttt-cell.ttt-o::after) instead of the font
    // glyph, which renders as an oval rather than a true circle.
    cell.textContent = mark === 'X' ? 'X' : '';
    cell.classList.add(mark === 'X' ? 'ttt-x' : 'ttt-o');
    cell.disabled = true;
    cell.setAttribute('aria-label', `Cell ${index + 1} of 9, ${mark}`);
}

function checkGameEnd() {
    const winLine = WIN_LINES.find(line => {
        const [a, b, c] = line;
        return board[a] && board[a] === board[b] && board[b] === board[c];
    });

    if (winLine) {
        gameOver = true;
        winLine.forEach(i => cells[i].classList.add('ttt-win'));
        cells.forEach(cell => { cell.disabled = true; });

        const winner = board[winLine[0]];
        setStatus(winner === humanMark ? 'You win!' : 'AI wins.');
        return true;
    }

    if (board.every(Boolean)) {
        gameOver = true;
        setStatus("It's a draw.");
        return true;
    }

    return false;
}

function setStatus(text) {
    statusEl.textContent = text;
}
