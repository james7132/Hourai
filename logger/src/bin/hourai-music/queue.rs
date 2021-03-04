use std::collections::VecDeque;
use rand::seq::SliceRandom;

pub struct QueueItem<K, V> {
    pub key: K,
    pub value: V
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
pub struct MusicQueue<K, V>(VecDeque<(K, VecDeque<V>)>);

impl<K, V> MusicQueue<K, V> where K: Copy + Eq {

    pub fn new() -> Self {
        Self(VecDeque::new())
    }

    /// Appends a value to the end of a key's queue.  If a key's queue does not exist, one will be
    /// created for it.
    ///
    /// If there are n keys already in the queue, this is a O(n) operation.
    pub fn push(&mut self, key: K, value: V) {
        self.extend(key, vec![value]);
    }

    /// Appends a full list of values to the end of a key's queue.  If a key's queue does not
    /// exist, one will be created for it.
    ///
    /// If there are n keys already in the queue and m values are being added to the queue, this
    /// is a O(n + m) operation.
    pub fn extend(&mut self, key: K, values: impl IntoIterator<Item=V>) {
        let values: VecDeque<V> = values.into_iter().collect();
        if self.contains_key(key) {
            self.0
                .iter_mut()
                .find(|kv| kv.0 == key)
                .map(|mut kv| {
                    kv.1.reserve(values.len());
                    for value in values {
                        kv.1.push_back(value);
                    }
                });
        } else {
            self.0.push_back((key, values));
        }
    }

    /// Pops the latest item from the queue. This is a O(1) operation.
    pub fn pop(&mut self) -> Option<QueueItem<K, V>> {
        let (key, value, size) = {
            let peek = self.0.get_mut(0)?;
            let value = peek.1.pop_front().expect("Individual user queues should be non-empty");
            (peek.0, value, peek.1.len())
        };

        if size > 0 {
            self.0.rotate_left(1);
        } else {
            self.0.pop_front();
        }

        Some(QueueItem {
            key: key,
            value: value
        })
    }

    /// Shuffles the items for a single key. If there are n keys and k values in the
    /// queue for a given key, this is a O(n + k) operation.
    ///
    /// A no-op if no items have the key in the queue.
    pub fn shuffle(&mut self, key: K) {
        self.0
            .iter_mut()
            .find(|kv| kv.0 == key)
            .map(|mut kv| {
                kv.1.make_contiguous().shuffle(&mut rand::thread_rng());
            });
    }

    /// Clears all of the items within a given the queue.
    /// If there are n keys in the queue, this is a O(n) operation.
    pub fn clear_key(&mut self, key: K) {
        self.0.retain(|r| r.0 != key);
    }

    /// Clears all of the items from the queue.
    /// This is a O(1) operation.
    pub fn clear(&mut self) {
        self.0.clear()
    }

    pub fn contains_key(&self, key: K) -> bool {
        self.0.iter().find(|kv| kv.0 == key).is_some()
    }

}
