<script setup>
import { computed } from 'vue'

const props = defineProps({
  currentPage: {
    type: Number,
    required: true,
  },
  totalPages: {
    type: Number,
    required: true,
  },
  totalCount: {
    type: Number,
    default: 0,
  },
  pageSize: {
    type: Number,
    default: 10,
  },
})

const emit = defineEmits(['update:currentPage', 'page-change'])

function buildPageList(current, total) {
  const delta = 2
  const pages = []
  const range = Array.from(new Set([1, total, current - delta, current, current + delta]))
    .filter((p) => p >= 1 && p <= total)
    .sort((a, b) => a - b)

  let prev = 0
  for (const p of range) {
    if (prev && p - prev > 1) pages.push('...')
    pages.push(p)
    prev = p
  }
  return pages
}

const pageList = computed(() => buildPageList(props.currentPage, props.totalPages))

const startRecord = computed(() => {
  if (props.totalCount === 0) return 0
  return (props.currentPage - 1) * props.pageSize + 1
})

const endRecord = computed(() => {
  return Math.min(props.currentPage * props.pageSize, props.totalCount)
})

function setPage(p) {
  if (p < 1 || p > props.totalPages || p === props.currentPage) return
  emit('update:currentPage', p)
  emit('page-change', p)
}
</script>

<template>
  <div v-if="totalCount > 0" class="tecnm-pagination">
    <span class="tecnm-pagination-info">
      Mostrando {{ startRecord }}–{{ endRecord }} de {{ totalCount }} registro(s)
    </span>
    <div class="tecnm-pagination-pages">
      <button
        type="button"
        class="tecnm-pagination-btn"
        :disabled="currentPage <= 1"
        aria-label="Página anterior"
        @click="setPage(currentPage - 1)"
      >
        &laquo;
      </button>

      <template v-for="(p, idx) in pageList" :key="idx">
        <span v-if="p === '...'" class="tecnm-pagination-ellipsis">…</span>
        <button
          v-else
          type="button"
          class="tecnm-pagination-btn"
          :class="{ active: p === currentPage }"
          :aria-current="p === currentPage ? 'page' : undefined"
          @click="setPage(p)"
        >
          {{ p }}
        </button>
      </template>

      <button
        type="button"
        class="tecnm-pagination-btn"
        :disabled="currentPage >= totalPages"
        aria-label="Página siguiente"
        @click="setPage(currentPage + 1)"
      >
        &raquo;
      </button>
    </div>
  </div>
</template>
