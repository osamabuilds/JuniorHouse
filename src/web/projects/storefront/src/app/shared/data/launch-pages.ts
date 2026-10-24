import { ContentPage } from '../models/site-settings.model';

/**
 * Seeded page drafts (wording only). Every business fact (delivery cities, times and charges, the exchange window,
 * contact details) is a `fact` block that shows the value entered in the admin app, and shows nothing until it exists.
 * Narrative copy is draft wording for the owner to review and replace in the admin app (SCRUM-206).
 */
export const launchPages: Readonly<Record<string, ContentPage>> = {
  delivery: {
    slug: 'delivery',
    title: 'Delivery',
    description: 'How your order travels from our hands to yours: where we deliver, how long it takes and what it costs.',
    status: 'published',
    blocks: [
      { type: 'paragraph', text: 'Every order is checked, folded and packed with care before it leaves us. We would rather take a moment longer than send anything out that is not right.' },
      { type: 'fact', key: 'delivery.rules', label: 'Where we deliver, and how long it takes' },
      { type: 'heading', level: 2, text: 'Tracking and questions' },
      { type: 'paragraph', text: 'If you would like an update on an order, message us and we will find out for you.' },
      { type: 'fact', key: 'contact.whatsapp', label: 'Message us' },
    ],
  },
  exchanges: {
    slug: 'exchanges',
    title: 'Exchanges',
    description: 'Children grow quickly and sizes can surprise. Here is how exchanges work.',
    status: 'published',
    blocks: [
      { type: 'paragraph', text: 'Children grow quickly, and a size that looked right on paper does not always fit on the day. If something is not right, we will help you put it right.' },
      { type: 'fact', key: 'exchange.window', label: 'Exchange window' },
      { type: 'fact', key: 'exchange.conditions', label: 'Conditions' },
      { type: 'heading', level: 2, text: 'Starting an exchange' },
      { type: 'paragraph', text: 'Message us with your order number and the item you would like to exchange, and we will take it from there.' },
      { type: 'fact', key: 'contact.whatsapp', label: 'Message us' },
    ],
  },
  'size-guide': {
    slug: 'size-guide',
    title: 'Size guide',
    description: 'How to measure your child and choose the right size.',
    status: 'published',
    blocks: [
      { type: 'paragraph', text: 'The right size is the one that lets a child move, climb and sit comfortably. Measure first, then choose.' },
      { type: 'heading', level: 2, text: 'How to measure' },
      { type: 'list', ordered: true, items: [
        'Ask your child to stand relaxed, in light clothing.',
        'Measure around the fullest part of the chest, keeping the tape level and snug, not tight.',
        'Measure around the natural waist, just above the navel.',
        'Measure height from the floor to the top of the head, standing against a wall.',
      ] },
      { type: 'heading', level: 2, text: 'Between two sizes' },
      { type: 'paragraph', text: 'If your child falls between two sizes, choose the larger one: there is room to grow, and sleeves and hems are easy to turn up.' },
      { type: 'callout', text: 'Each product page carries its own size chart, so you can check the exact measurements before you order.' },
    ],
  },
  'our-story': {
    slug: 'our-story',
    title: 'Our story',
    description: 'Why we make clothes for children, and how we think about them.',
    status: 'published',
    blocks: [
      { type: 'paragraph', text: 'Romp makes clothes for children who run, climb, spin and sit on the floor. We think clothes for the young should be comfortable first, beautiful second, and built to be handed down.' },
      { type: 'heading', level: 2, text: 'Quietly made' },
      { type: 'paragraph', text: 'We keep our collections small and our colours calm. A few good pieces, chosen with care, beat a wardrobe of things that will not last the season.' },
      { type: 'heading', level: 2, text: 'For families' },
      { type: 'paragraph', text: 'We build Romp around how families actually shop: clear sizes, honest descriptions and a person to talk to when you need one.' },
    ],
  },
  'made-in-pakistan': {
    slug: 'made-in-pakistan',
    title: 'Made in Pakistan',
    description: 'How our clothes are made, and the people and standards behind them.',
    status: 'published',
    blocks: [
      { type: 'paragraph', text: 'Our clothes are made in Pakistan, by people who know cloth. We work with our makers closely, and every piece is checked before it is ever offered to you.' },
      { type: 'heading', level: 2, text: 'Checked before it reaches you' },
      { type: 'paragraph', text: 'Each garment passes a quality check, is labelled and counted into our warehouse, and is packed by hand when you order.' },
      { type: 'heading', level: 2, text: 'Fabric and finish' },
      { type: 'paragraph', text: 'Fabric and care details are shown on every product page, so you know exactly what your child will be wearing and how to look after it.' },
    ],
  },
  contact: {
    slug: 'contact',
    title: 'Contact',
    description: 'Talk to us about an order, a size or anything else.',
    status: 'published',
    blocks: [
      { type: 'paragraph', text: 'We are glad to help with an order, a size or a question. The quickest way to reach us is by message.' },
      { type: 'fact', key: 'contact.whatsapp', label: 'WhatsApp' },
      { type: 'fact', key: 'contact.email', label: 'Email' },
      { type: 'fact', key: 'contact.hours', label: 'Hours' },
    ],
  },
};
