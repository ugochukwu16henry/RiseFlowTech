import { useEffect } from 'react';
import { useLocation } from 'react-router-dom';
import { getRouteSeo, seoDefaults } from './routeSeo';

function upsertMeta(name, content, attr = 'name') {
  if (!content) return;
  const selector = `meta[${attr}="${name}"]`;
  let node = document.head.querySelector(selector);
  if (!node) {
    node = document.createElement('meta');
    node.setAttribute(attr, name);
    document.head.appendChild(node);
  }
  node.setAttribute('content', content);
}

function upsertLink(rel, href) {
  if (!href) return;
  const selector = `link[rel="${rel}"]`;
  let node = document.head.querySelector(selector);
  if (!node) {
    node = document.createElement('link');
    node.setAttribute('rel', rel);
    document.head.appendChild(node);
  }
  node.setAttribute('href', href);
}

function upsertJsonLd(schemaList) {
  const id = 'route-jsonld';
  let node = document.head.querySelector(`#${id}`);
  if (!node) {
    node = document.createElement('script');
    node.id = id;
    node.type = 'application/ld+json';
    document.head.appendChild(node);
  }
  node.textContent = JSON.stringify(schemaList.length === 1 ? schemaList[0] : schemaList);
}

export default function RouteSeoManager() {
  const location = useLocation();

  useEffect(() => {
    const siteBase = (import.meta.env.VITE_PUBLIC_SITE_URL || window.location.origin).replace(/\/+$/, '');
    const pathname = location.pathname || '/';
    const seo = getRouteSeo(pathname, siteBase);

    const canonicalUrl = `${siteBase}${seo.canonicalPath}`;
    const ogImage = seo.image.startsWith('http') ? seo.image : `${siteBase}${seo.image}`;

    document.title = seo.title || seoDefaults.title;
    upsertMeta('description', seo.description || seoDefaults.description);
    upsertMeta('keywords', seo.keywords || '');
    upsertMeta('robots', seo.index ? 'index,follow,max-image-preview:large' : 'noindex,nofollow');
    upsertMeta('og:site_name', seoDefaults.siteName, 'property');
    upsertMeta('og:type', seo.type || 'website', 'property');
    upsertMeta('og:title', seo.title || seoDefaults.title, 'property');
    upsertMeta('og:description', seo.description || seoDefaults.description, 'property');
    upsertMeta('og:url', canonicalUrl, 'property');
    upsertMeta('og:image', ogImage, 'property');
    upsertMeta('twitter:card', 'summary_large_image');
    upsertMeta('twitter:title', seo.title || seoDefaults.title);
    upsertMeta('twitter:description', seo.description || seoDefaults.description);
    upsertMeta('twitter:image', ogImage);
    upsertLink('canonical', canonicalUrl);

    if (seo.schema?.length) {
      upsertJsonLd(seo.schema);
      return;
    }
    const schemaNode = document.head.querySelector('#route-jsonld');
    if (schemaNode) schemaNode.remove();
  }, [location.pathname]);

  return null;
}
