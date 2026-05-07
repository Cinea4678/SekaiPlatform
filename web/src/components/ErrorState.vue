<script setup lang="ts">
import type { ApiError } from '@/api/client'
import { AlertCircle } from 'lucide-vue-next'
import Button from '@/components/ui/Button.vue'

defineProps<{
  error: ApiError
}>()

defineEmits<{
  retry: []
}>()
</script>

<template>
  <div class="rounded-md border border-destructive/30 bg-destructive/5 p-5">
    <div class="flex gap-3 text-destructive">
      <AlertCircle :size="20" class="mt-0.5 shrink-0" />
      <div class="min-w-0">
        <p class="text-sm font-medium">
          {{ error.message }}
        </p>
        <p v-if="error.traceId" class="mt-2 text-xs text-muted-foreground">
          trace_id: {{ error.traceId }}
        </p>
      </div>
    </div>
    <Button class="mt-4" variant="outline" size="sm" @click="$emit('retry')">
      重试
    </Button>
  </div>
</template>
