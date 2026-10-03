import React from 'react';
import ReactDOM from 'react-dom/client';
import App from './App';
import './style.css';
ReactDOM.createRoot(document.getElementById('root')!).render(<React.StrictMode><App /></React.StrictMode>);
// Register only production builds, avoiding stale assets during development.
if (import.meta.env.PROD && 'serviceWorker' in navigator) {
  window.addEventListener('load', () => { void navigator.serviceWorker.register('/sw.js').catch(() => console.warn('安裝快取暫時無法啟用，線上記帳功能不受影響。')); });
}
