import {deduplicateTagsByLabel} from './track-details.component';

describe('deduplicateTagsByLabel', () => {
  it('keeps the highest-scoring tag for each case-insensitive displayed label', () => {
    const result = deduplicateTagsByLabel([
      {namespace: 'genre', label: 'hiphop', score: 0.61},
      {namespace: 'style', label: 'HipHop', score: 0.88},
      {namespace: 'genre', label: 'rock', score: 0.74},
      {namespace: 'mood', label: ' rock ', score: 0.42},
      {namespace: 'genre', label: 'pop', score: 0.67}
    ]);

    expect(result).toEqual([
      {namespace: 'style', label: 'HipHop', score: 0.88},
      {namespace: 'genre', label: 'rock', score: 0.74},
      {namespace: 'genre', label: 'pop', score: 0.67}
    ]);
  });
});
