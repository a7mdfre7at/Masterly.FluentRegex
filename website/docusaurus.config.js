// @ts-check
// Docusaurus configuration for the Masterly.FluentRegex documentation site.
// See: https://docusaurus.io/docs/api/docusaurus-config

import fs from 'node:fs';
import path from 'node:path';
import {fileURLToPath} from 'node:url';
import {themes as prismThemes} from 'prism-react-renderer';

const repositoryUrl = 'https://github.com/a7mdfre7at/Masterly.FluentRegex';
const siteUrl = 'https://a7mdfre7at.github.io';
const baseUrl = '/Masterly.FluentRegex/';
const socialImageUrl = 'https://raw.githubusercontent.com/a7mdfre7at/Masterly.FluentRegex/master/repo_image.png';

// The playground is a separate Blazor WebAssembly app, published next to this site under /playground/.
const playgroundUrl = `${siteUrl}${baseUrl}playground/`;

// Every package ships with one version. The core project file is its source of truth.
const siteDirectory = path.dirname(fileURLToPath(import.meta.url));
const coreProject = fs.readFileSync(
  path.join(siteDirectory, '..', 'src', 'Masterly.FluentRegex', 'Masterly.FluentRegex.csproj'),
  'utf8',
);
const versionMatch = /<Version>([^<]+)<\/Version>/.exec(coreProject);
if (!versionMatch) {
  throw new Error('src/Masterly.FluentRegex/Masterly.FluentRegex.csproj has no <Version> element.');
}
const packageVersion = versionMatch[1].trim();
const majorMinor = packageVersion.split('.').slice(0, 2).join('.');

/** @type {import('@docusaurus/types').Config} */
const config = {
  title: 'Masterly.FluentRegex',
  tagline: 'Regular expressions you can read, prove and ship.',
  favicon: 'img/favicon.svg',

  future: {
    v4: true,
  },

  url: siteUrl,
  baseUrl,
  organizationName: 'a7mdfre7at',
  projectName: 'Masterly.FluentRegex',
  trailingSlash: false,

  onBrokenLinks: 'throw',
  onBrokenAnchors: 'throw',

  customFields: {
    packageVersion,
    repositoryUrl,
    playgroundUrl,
  },

  markdown: {
    // .md files are plain CommonMark, so C# generics such as Template<LogLine> and regex braces need no escaping.
    format: 'detect',
    hooks: {
      onBrokenMarkdownLinks: 'throw',
    },
  },

  headTags: [
    {tagName: 'link', attributes: {rel: 'preconnect', href: 'https://fonts.googleapis.com'}},
    {tagName: 'link', attributes: {rel: 'preconnect', href: 'https://fonts.gstatic.com', crossorigin: 'anonymous'}},
  ],

  stylesheets: [
    'https://fonts.googleapis.com/css2?family=IBM+Plex+Sans:ital,wght@0,400;0,500;0,600;0,700;1,400&family=JetBrains+Mono:wght@400;500;600&display=swap',
  ],

  i18n: {
    defaultLocale: 'en',
    locales: ['en'],
  },

  presets: [
    [
      'classic',
      /** @type {import('@docusaurus/preset-classic').Options} */
      ({
        docs: {
          sidebarPath: './sidebars.js',
          editUrl: `${repositoryUrl}/tree/master/website/`,
          showLastUpdateTime: true,
        },
        blog: false,
        theme: {
          customCss: './src/css/custom.css',
        },
      }),
    ],
  ],

  themes: [
    [
      '@easyops-cn/docusaurus-search-local',
      /** @type {import('@easyops-cn/docusaurus-search-local').PluginOptions} */
      ({
        hashed: true,
        indexBlog: false,
        docsRouteBasePath: '/docs',
        highlightSearchTermsOnTargetPage: true,
        explicitSearchResultPath: true,
        searchResultLimits: 10,
        searchBarShortcutHint: true,
      }),
    ],
  ],

  themeConfig:
    /** @type {import('@docusaurus/preset-classic').ThemeConfig} */
    ({
      image: socialImageUrl,
      metadata: [
        {
          name: 'keywords',
          content:
            '.NET, C#, regex, regular expressions, fluent API, regex builder, ReDoS, regex equivalence, EF Core regex, source generator',
        },
      ],
      colorMode: {
        defaultMode: 'dark',
        respectPrefersColorScheme: true,
      },
      announcementBar: {
        id: `release-${majorMinor}`,
        content: `<strong>${majorMinor} is out.</strong> Pattern algebra, a verified optimizer, SQL regex dialects and the EF Core package. <a href="${baseUrl}docs/overview">See what's new →</a>`,
        isCloseable: true,
      },
      docs: {
        sidebar: {
          hideable: true,
          autoCollapseCategories: false,
        },
      },
      tableOfContents: {
        minHeadingLevel: 2,
        maxHeadingLevel: 3,
      },
      navbar: {
        title: 'Masterly.FluentRegex',
        hideOnScroll: false,
        logo: {
          alt: '',
          src: 'img/logo.svg',
          width: 28,
          height: 28,
        },
        items: [
          {type: 'docSidebar', sidebarId: 'docs', position: 'left', label: 'Docs'},
          {to: '/docs/examples', label: 'Examples', position: 'left'},
          {to: '/docs/api-reference', label: 'API', position: 'left'},
          {href: playgroundUrl, label: 'Playground', position: 'left'},
          {
            type: 'html',
            position: 'right',
            value: `<a class="navbar-version" href="${repositoryUrl}/blob/master/CHANGELOG.md" title="Changelog">v${packageVersion}</a>`,
          },
          {
            href: 'https://www.nuget.org/packages/Masterly.FluentRegex',
            position: 'right',
            className: 'navbar-icon-link navbar-icon-link--nuget',
            'aria-label': 'NuGet package',
          },
          {
            href: repositoryUrl,
            position: 'right',
            className: 'navbar-icon-link navbar-icon-link--github',
            'aria-label': 'GitHub repository',
          },
        ],
      },
      footer: {
        style: 'dark',
        links: [
          {
            title: 'Learn',
            items: [
              {label: 'Quick Start', to: '/docs/quick-start'},
              {label: 'Core Concepts', to: '/docs/core-concepts'},
              {label: 'Real-World Examples', to: '/docs/examples'},
              {label: 'Playground', href: playgroundUrl},
            ],
          },
          {
            title: 'Reference',
            items: [
              {label: 'API Reference', to: '/docs/api-reference'},
              {label: 'Regex Cheat Sheet', to: '/docs/regex-cheat-sheet'},
              {label: 'Upgrading from 2.x', to: '/docs/upgrading-from-2x'},
              {label: 'Changelog', href: `${repositoryUrl}/blob/master/CHANGELOG.md`},
            ],
          },
          {
            title: 'Project',
            items: [
              {label: 'GitHub', href: repositoryUrl},
              {label: 'Issues', href: `${repositoryUrl}/issues`},
              {label: 'NuGet', href: 'https://www.nuget.org/packages?q=Masterly.FluentRegex'},
            ],
          },
        ],
        copyright: `MIT licensed · © ${new Date().getFullYear()} Ahmad Omar Al-Freihat`,
      },
      prism: {
        theme: prismThemes.oneLight,
        darkTheme: prismThemes.oneDark,
        additionalLanguages: ['csharp', 'bash', 'json', 'powershell', 'sql', 'regex'],
      },
    }),
};

export default config;
