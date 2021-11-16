use rand::seq::SliceRandom;
use std::collections::VecDeque;

#[derive(Debug, Eq, PartialEq)]
pub struct QueueItem<K, V> {
    pub key: K,
    pub value: V,
}

/// A FIFO, round-robin key based queue.  The input is be a pair of key, value pairs.
/// If a queue has n keys and k values, most operations on this data structure runs in O(n) time.
///
/// Example:
///   Input: (a, 1), (a, 2), (b, 1), (c, 1), (a, 3), (b, 2)
///   Output: (a, 1), (b, 1), (c, 1), (a, 2), (b, 2), (a, 3)
///
/// This structure is thread-safe and does not implement Send or Sync. Wrapping it in a RwLock is
/// highly suggested.
#[derive(Clone)]
pub struct MusicQueue<K, V>(VecDeque<(K, VecDeque<V>)>);

impl<K, V> MusicQueue<K, V>
where
    K: Copy + Eq,
{
    pub fn new() -> Self {
        Self(VecDeque::new())
    }

    pub fn push(&mut self, key: K, value: V) {
        if let Some(kv) = self.0.iter_mut().find(|kv| kv.0 == key) {
            kv.1.push_back(value);
        } else {
            self.0.push_back((key, VecDeque::from(vec![value])));
        }
    }

    /// Appends a full list of values to the end of a key's queue.  If a key's queue does not
    /// exist, one will be created for it.
    ///
    /// If there are n keys already in the queue and m values are being added to the queue, this
    /// is a O(n + m) operation.
    pub fn extend(&mut self, key: K, values: impl IntoIterator<Item = V>) {
        let values: VecDeque<V> = values.into_iter().collect();
        if let Some(kv) = self.0.iter_mut().find(|kv| kv.0 == key) {
            kv.1.reserve(values.len());
            for value in values {
                kv.1.push_back(value);
            }
        } else {
            self.0.push_back((key, values));
        }
    }

    /// Peeks at the first item in the queue. This is a O(1) operation.
    pub fn peek(&self) -> Option<QueueItem<K, &V>> {
        let queue_peek = self.0.get(0)?;
        Some(QueueItem {
            key: queue_peek.0,
            value: queue_peek.1.get(0)?,
        })
    }

    /// Pops the latest item from the queue. This is a O(1) operation.
    pub fn pop(&mut self) -> Option<QueueItem<K, V>> {
        let (key, value, size) = {
            let peek = self.0.get_mut(0)?;
            let value = peek
                .1
                .pop_front()
                .expect("Individual user queues should be non-empty");
            (peek.0, value, peek.1.len())
        };

        if size > 0 {
            self.0.rotate_left(1);
        } else {
            self.0.pop_front();
        }

        Some(QueueItem { key, value })
    }

    /// Gets the total number of items in the queue. If there are n keys and k values in the queue
    /// for a given key, this is a O(n) operation.
    pub fn len(&self) -> usize {
        self.0.iter().map(|kv| kv.1.len()).sum()
    }

    /// Gets the total number of items in the queue for a given key.  If there are n keys and k
    /// values in the queue for a given key, this is a O(n) operation.
    pub fn count(&self, key: K) -> Option<usize> {
        self.0.iter().find(|kv| kv.0 == key).map(|kv| kv.1.len())
    }

    /// Shuffles the items for a single key. If there are n keys and k values in the
    /// queue for a given key, this is a O(n + k) operation.
    ///
    /// A no-op if no items have the key in the queue.
    ///
    /// Returns the number of items shuffled in the queue.
    pub fn shuffle(&mut self, key: K) -> Option<usize> {
        self.0.iter_mut().find(|kv| kv.0 == key).map(|kv| {
            kv.1.make_contiguous().shuffle(&mut rand::thread_rng());
            kv.1.len()
        })
    }

    /// Gets the item at the nth index in the queue, if found.
    /// Runs in O(n) time if n items are in the queue.
    pub fn get(&mut self, idx: usize) -> Option<QueueItem<K, &V>> {
        self.iter().nth(idx)
    }

    /// Removes the item at the nth index in the queue and returns it, if found.
    /// Runs in O(n) time if n items are in the queue.
    pub fn remove(&mut self, idx: usize) -> Option<V> {
        let (k, b) = self.index_iter().nth(idx)?;
        let bucket = self.0.get_mut(k).unwrap();
        let retval = bucket.1.remove(b).unwrap();
        if bucket.1.len() == 0 {
            self.0.remove(k);
        }
        Some(retval)
    }

    /// Clears all of the items with a given key in the queue.
    /// If there are n keys in the queue, this is a O(n) operation.
    ///
    /// Returns the number of items removed from the queue.
    pub fn clear_key(&mut self, key: K) -> Option<usize> {
        self.count(key).map(|count| {
            self.0.retain(|r| r.0 != key);
            count
        })
    }

    /// Clears all of the items within a given the queue.
    /// If there are n keys in the queue, this is a O(1) operation.
    pub fn clear(&mut self) {
        self.0.clear()
    }

    pub fn contains_key(&mut self, key: K) -> bool {
        self.0.iter().find(|kv| kv.0 == key).is_some()
    }

    fn index_iter<'a>(&'a self) -> MusicQueueIndexer<'a, K, V> {
        MusicQueueIndexer {
            queue: self,
            key_idx: 0,
            bucket_idx: 0,
            max_bucket_idx: self.0.iter().map(|kv| kv.1.len()).max().unwrap_or(0),
        }
    }

    pub fn iter<'a>(&'a self) -> MusicQueueIterator<'a, K, V> {
        MusicQueueIterator {
            indexer: self.index_iter(),
        }
    }

    pub fn keys(&self) -> impl Iterator<Item = K> + '_ {
        self.0.iter().map(|(k, _)| *k)
    }

    pub fn iter_with_key(&self, key: K) -> Vec<&V> {
        self.0
            .iter()
            .find(|(k, _)| *k == key)
            .map(|(_, queue)| queue.iter().collect())
            .unwrap_or_else(|| Vec::new())
    }
}

struct MusicQueueIndexer<'a, K, V> {
    queue: &'a MusicQueue<K, V>,
    key_idx: usize,
    bucket_idx: usize,
    max_bucket_idx: usize,
}

pub struct MusicQueueIterator<'a, K, V> {
    indexer: MusicQueueIndexer<'a, K, V>,
}

impl<'a, K: Copy, V> Iterator for MusicQueueIndexer<'a, K, V> {
    type Item = (usize, usize);
    fn next(&mut self) -> Option<Self::Item> {
        if self.bucket_idx > self.max_bucket_idx {
            return None;
        }
        loop {
            let item = self
                .queue
                .0
                .get(self.key_idx)
                .and_then(|kv| kv.1.get(self.bucket_idx));
            let retval = item.map(|_| (self.key_idx, self.bucket_idx));
            self.key_idx += 1;
            if self.key_idx >= self.queue.0.len() {
                self.key_idx = 0;
                self.bucket_idx += 1;
            }
            if retval.is_some() {
                return retval;
            } else if self.bucket_idx > self.max_bucket_idx {
                return None;
            }
        }
    }
}

impl<'a, K: Copy, V> Iterator for MusicQueueIterator<'a, K, V> {
    type Item = QueueItem<K, &'a V>;
    fn next(&mut self) -> Option<Self::Item> {
        let (k, b) = self.indexer.next()?;
        let (key, bucket) = self.indexer.queue.0.get(k).unwrap();
        Some(QueueItem {
            key: *key,
            value: bucket.get(b).unwrap(),
        })
    }
}

#[cfg(test)]
mod test {
    use super::{MusicQueue, QueueItem};

    #[test]
    fn test_queue_push() {
        let mut queue: MusicQueue<u64, u64> = MusicQueue::new();
        queue.push(20, 20);
        assert_eq!(queue.len(), 1);
        assert_eq!(queue.count(20), Some(1));
        assert_eq!(queue.count(40), None);
    }

    #[test]
    fn test_queue_pop() {
        let mut queue: MusicQueue<u64, u64> = MusicQueue::new();
        queue.push(20, 20);
        let result = queue.pop();
        assert_eq!(result, Some(QueueItem { key: 20, value: 20 }));
        assert_eq!(queue.len(), 0);
        assert_eq!(queue.count(20), None);
        assert_eq!(queue.count(40), None);
    }

    #[test]
    fn test_queue_pop_empty() {
        let mut queue: MusicQueue<u64, u64> = MusicQueue::new();
        assert_eq!(queue.pop(), None);
        assert_eq!(queue.len(), 0);
        assert_eq!(queue.count(20), None);
        assert_eq!(queue.count(40), None);
        assert_eq!(queue.pop(), None);
        assert_eq!(queue.pop(), None);
        assert_eq!(queue.pop(), None);
        assert_eq!(queue.pop(), None);
    }

    #[test]
    fn test_queue_extend() {
        let mut queue: MusicQueue<u64, u64> = MusicQueue::new();
        queue.extend(20, vec![20, 40, 60]);
        assert_eq!(queue.len(), 3);
        assert_eq!(queue.count(20), Some(3));
        assert_eq!(queue.count(40), None);
        assert_eq!(queue.pop(), Some(QueueItem { key: 20, value: 20 }));
        assert_eq!(queue.len(), 2);
        assert_eq!(queue.count(20), Some(2));
        assert_eq!(queue.count(40), None);
        assert_eq!(queue.pop(), Some(QueueItem { key: 20, value: 40 }));
        assert_eq!(queue.len(), 1);
        assert_eq!(queue.count(20), Some(1));
        assert_eq!(queue.count(40), None);
        assert_eq!(queue.pop(), Some(QueueItem { key: 20, value: 60 }));
        assert_eq!(queue.len(), 0);
        assert_eq!(queue.count(20), None);
        assert_eq!(queue.count(40), None);
    }

    #[test]
    fn test_queue_clear() {
        let mut queue: MusicQueue<u64, u64> = MusicQueue::new();
        queue.extend(20, vec![20, 40, 60]);
        queue.extend(40, vec![20, 40, 60]);
        queue.extend(60, vec![20, 40, 60]);
        assert_eq!(queue.len(), 9);
        assert_eq!(queue.count(20), Some(3));
        assert_eq!(queue.count(40), Some(3));
        assert_eq!(queue.count(60), Some(3));
        queue.clear();
        assert_eq!(queue.len(), 0);
        assert_eq!(queue.count(20), None);
        assert_eq!(queue.count(40), None);
        assert_eq!(queue.count(60), None);
    }

    #[test]
    fn test_queue_get() {
        let mut queue: MusicQueue<u64, u64> = MusicQueue::new();
        queue.push(20, 20);
        queue.push(10, 10);
        queue.push(5, 30);
        queue.push(10, 15);
        queue.push(20, 40);
        queue.push(20, 60);
        assert_eq!(
            queue.get(0),
            Some(QueueItem {
                key: 20,
                value: &20
            })
        );
        assert_eq!(
            queue.get(1),
            Some(QueueItem {
                key: 10,
                value: &10
            })
        );
        assert_eq!(queue.get(2), Some(QueueItem { key: 5, value: &30 }));
        assert_eq!(
            queue.get(3),
            Some(QueueItem {
                key: 20,
                value: &40
            })
        );
        assert_eq!(
            queue.get(4),
            Some(QueueItem {
                key: 10,
                value: &15
            })
        );
        assert_eq!(
            queue.get(5),
            Some(QueueItem {
                key: 20,
                value: &60
            })
        );
        assert_eq!(queue.get(6), None);
        assert_eq!(queue.get(7), None);
        assert_eq!(queue.len(), 6);
    }

    #[test]
    fn test_queue_remove() {
        let mut queue: MusicQueue<u64, u64> = MusicQueue::new();
        queue.push(20, 20);
        queue.push(10, 10);
        queue.push(5, 30);
        queue.push(10, 15);
        queue.push(20, 40);
        queue.push(20, 60);
        assert_eq!(queue.remove(3), Some(40));
        assert_eq!(queue.count(20), Some(2));
        assert_eq!(queue.count(10), Some(2));
        assert_eq!(queue.count(5), Some(1));
        assert_eq!(queue.remove(300), None);
    }

    #[test]
    fn test_queue_round_robin() {
        let mut queue: MusicQueue<u64, u64> = MusicQueue::new();
        queue.push(20, 20);
        queue.push(10, 10);
        queue.push(5, 30);
        queue.push(10, 15);
        queue.push(20, 40);
        queue.push(20, 60);
        assert_eq!(queue.len(), 6);
        assert_eq!(queue.count(20), Some(3));
        assert_eq!(queue.count(10), Some(2));
        assert_eq!(queue.count(5), Some(1));
        assert_eq!(queue.pop(), Some(QueueItem { key: 20, value: 20 }));
        assert_eq!(queue.len(), 5);
        assert_eq!(queue.count(20), Some(2));
        assert_eq!(queue.count(10), Some(2));
        assert_eq!(queue.count(5), Some(1));
        assert_eq!(queue.pop(), Some(QueueItem { key: 10, value: 10 }));
        assert_eq!(queue.len(), 4);
        assert_eq!(queue.count(20), Some(2));
        assert_eq!(queue.count(10), Some(1));
        assert_eq!(queue.count(5), Some(1));
        assert_eq!(queue.pop(), Some(QueueItem { key: 5, value: 30 }));
        assert_eq!(queue.len(), 3);
        assert_eq!(queue.count(20), Some(2));
        assert_eq!(queue.count(10), Some(1));
        assert_eq!(queue.count(5), None);
        assert_eq!(queue.pop(), Some(QueueItem { key: 20, value: 40 }));
        assert_eq!(queue.len(), 2);
        assert_eq!(queue.count(20), Some(1));
        assert_eq!(queue.count(10), Some(1));
        assert_eq!(queue.count(5), None);
        assert_eq!(queue.pop(), Some(QueueItem { key: 10, value: 15 }));
        assert_eq!(queue.len(), 1);
        assert_eq!(queue.count(20), Some(1));
        assert_eq!(queue.count(10), None);
        assert_eq!(queue.count(5), None);
        assert_eq!(queue.pop(), Some(QueueItem { key: 20, value: 60 }));
        assert_eq!(queue.len(), 0);
        assert_eq!(queue.count(20), None);
        assert_eq!(queue.count(10), None);
        assert_eq!(queue.count(5), None);
    }

    #[test]
    fn test_queue_iter() {
        let mut queue: MusicQueue<u64, u64> = MusicQueue::new();
        queue.push(20, 20);
        queue.push(20, 40);
        queue.push(10, 10);
        queue.push(5, 30);
        queue.push(10, 15);
        queue.push(20, 60);
        let mut iter = queue.iter();
        assert_eq!(
            iter.next(),
            Some(QueueItem {
                key: 20,
                value: &20
            })
        );
        assert_eq!(
            iter.next(),
            Some(QueueItem {
                key: 10,
                value: &10
            })
        );
        assert_eq!(iter.next(), Some(QueueItem { key: 5, value: &30 }));
        assert_eq!(
            iter.next(),
            Some(QueueItem {
                key: 20,
                value: &40
            })
        );
        assert_eq!(
            iter.next(),
            Some(QueueItem {
                key: 10,
                value: &15
            })
        );
        assert_eq!(
            iter.next(),
            Some(QueueItem {
                key: 20,
                value: &60
            })
        );
        assert_eq!(iter.next(), None);
        assert_eq!(iter.next(), None);
        assert_eq!(iter.next(), None);
    }
}
