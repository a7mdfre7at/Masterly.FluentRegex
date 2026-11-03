import React, {useEffect, useRef, useState} from 'react';
import clsx from 'clsx';
import Icon from './Icon';
import styles from './CopyCommand.module.css';

const resetDelayMs = 2000;

/**
 * A shell command with a copy button. The button confirms the copy visually and to screen readers.
 */
export default function CopyCommand({command, className}) {
  const [status, setStatus] = useState('idle');
  const timer = useRef(null);

  useEffect(() => () => clearTimeout(timer.current), []);

  async function copy() {
    try {
      await navigator.clipboard.writeText(command);
      setStatus('copied');
    } catch {
      setStatus('failed');
    }
    clearTimeout(timer.current);
    timer.current = setTimeout(() => setStatus('idle'), resetDelayMs);
  }

  return (
    <div className={clsx(styles.command, className)}>
      <span className={styles.prompt} aria-hidden="true">
        $
      </span>
      <code className={styles.text}>{command}</code>
      <button
        type="button"
        className={clsx(styles.button, status === 'copied' && styles.copied)}
        onClick={copy}
        aria-label={`Copy command: ${command}`}>
        <Icon name={status === 'copied' ? 'check' : 'copy'} size={16} />
      </button>
      <span className={styles.srOnly} role="status" aria-live="polite">
        {status === 'copied' ? 'Copied to clipboard' : status === 'failed' ? 'Copy failed' : ''}
      </span>
    </div>
  );
}
