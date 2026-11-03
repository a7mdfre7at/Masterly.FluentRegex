import React from 'react';
import clsx from 'clsx';
import Link from '@docusaurus/Link';
import Icon from '@site/src/components/home/Icon';
import styles from './LinkCards.module.css';

/**
 * A responsive grid of linked cards for use inside MDX pages.
 * items: [{title, text, to, icon?, eyebrow?}]
 */
export default function LinkCards({items, columns = 3}) {
  return (
    <ul className={clsx(styles.grid, styles[`columns${columns}`])}>
      {items.map((item) => (
        <li key={item.to + item.title} className={styles.item}>
          <Link to={item.to} className={styles.card}>
            {item.icon && (
              <span className={styles.icon}>
                <Icon name={item.icon} size={18} />
              </span>
            )}
            {item.eyebrow && <span className={styles.eyebrow}>{item.eyebrow}</span>}
            <span className={styles.title}>
              {item.title}
              <Icon name="arrowRight" size={14} className={styles.arrow} />
            </span>
            <span className={styles.text}>{item.text}</span>
          </Link>
        </li>
      ))}
    </ul>
  );
}
