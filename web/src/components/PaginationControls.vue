<script setup lang="ts">
import type { PageMeta } from '@/api/types'
import { computed } from 'vue'
import Button from '@/components/ui/Button.vue'

const props = defineProps<{
  page: PageMeta
}>()

const emit = defineEmits<{
  change: [page: number]
}>()

const totalPages = computed(() => Math.max(1, Math.ceil(props.page.total / props.page.pageSize)))
const start = computed(() => props.page.total === 0 ? 0 : (props.page.page - 1) * props.page.pageSize + 1)
const end = computed(() => Math.min(props.page.total, props.page.page * props.page.pageSize))
</script>

<template>
  <div class="flex flex-col gap-3 border-t pt-4 text-sm text-muted-foreground sm:flex-row sm:items-center sm:justify-between">
    <p>
      {{ start }}-{{ end }} / {{ page.total }}
    </p>
    <div class="flex items-center gap-2">
      <Button
        variant="outline"
        size="sm"
        :disabled="page.page <= 1"
        @click="emit('change', page.page - 1)"
      >
        上一页
      </Button>
      <span class="min-w-16 text-center">
        {{ page.page }} / {{ totalPages }}
      </span>
      <Button
        variant="outline"
        size="sm"
        :disabled="page.page >= totalPages"
        @click="emit('change', page.page + 1)"
      >
        下一页
      </Button>
    </div>
  </div>
</template>
