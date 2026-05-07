<script setup lang="ts">
import { Loader2 } from 'lucide-vue-next'

defineProps<{
  label?: string
}>()
</script>

<template>
  <div class="flex min-h-48 items-center justify-center rounded-md border bg-card">
    <div class="flex items-center gap-2 text-sm text-muted-foreground">
      <Loader2 :size="18" class="animate-spin" />
      <span>{{ label || '加载中' }}</span>
    </div>
  </div>
</template>
